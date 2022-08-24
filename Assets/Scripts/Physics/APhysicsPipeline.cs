using System.Collections.Generic;
using Antares.Graphics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.ARenderUtilities;
using static Antares.Graphics.AShaderSpecifications;
using static Antares.Graphics.AShaderSpecifications.FluidSolverCompute;

namespace Antares.Physics
{
    public class APhysicsPipeline
    {
        public class EmitterBufferBuilder
        {
            public int TotalParticleCount { get; private set; }

            public int PartitionCount => PartitionBuffer.Count - 1;

            public List<FluidEmitterDispatch> EmitterDispatchBuffer { get; private set; }

            public List<int> PartitionBuffer { get; private set; }

            public List<byte> PropertyBuffer { get; private set; }

            public bool Submitted { get; private set; }

            private int _groupSpace;

            public EmitterBufferBuilder()
            {
                EmitterDispatchBuffer = new List<FluidEmitterDispatch>();
                PartitionBuffer = new List<int>();
                PropertyBuffer = new List<byte>();

                Clear();
            }

            public void AddEmitter<T>(T emitter) where T : IFluidEmitter
            {
                Debug.Assert(!Submitted);

                int propertyByteOffset = PropertyBuffer.Count;
                emitter.GetProperties(PropertyBuffer);

                int particleCount = emitter.ParticleCount;
                TotalParticleCount += particleCount;

                FluidEmitterType type = emitter.EmitterType;
                if (particleCount >= _groupSpace)
                {
                    int remainder = (particleCount - _groupSpace) % MaxEmitterParticleCountPerGroup;
                    for (int i = (particleCount - remainder + MaxEmitterParticleCountPerGroup - 1) / MaxEmitterParticleCountPerGroup; i > 0; i--)
                    {
                        EmitterDispatchBuffer.Add(new FluidEmitterDispatch(type, propertyByteOffset, MaxEmitterParticleCountPerGroup));
                        PartitionBuffer.Add(EmitterDispatchBuffer.Count);
                    }

                    particleCount = remainder;
                    _groupSpace = MaxEmitterParticleCountPerGroup;
                }

                if (particleCount > 0)
                {
                    _groupSpace -= particleCount;
                    EmitterDispatchBuffer.Add(new FluidEmitterDispatch(type, propertyByteOffset, MaxEmitterParticleCountPerGroup - _groupSpace));
                }
            }

            public void AddEmitter<T>(List<T> emitters) where T : IFluidEmitter
            {
                for (int i = 0; i < emitters.Count; i++)
                    AddEmitter(emitters[i]);
            }

            public void Submit()
            {
                PartitionBuffer.Add(EmitterDispatchBuffer.Count);
                Submitted = true;
            }

            public void Clear()
            {
                TotalParticleCount = 0;
                Submitted = false;
                _groupSpace = MaxEmitterParticleCountPerGroup;

                EmitterDispatchBuffer.Clear();
                PartitionBuffer.Clear();
                PropertyBuffer.Clear();

                PartitionBuffer.Add(0);
            }
        }

        public bool IsSceneLoaded { get; private set; } = false;

        public int MaxAddParticleCount { get => 1024; }

        private APhysicsScene _physicsScene;

        private ARenderPipeline _renderPipeline;
        private AShaderSpecifications _shaderSpecs;

        private ComputeBuffer _fluidParticlePositionsBuffer;
        private ComputeBuffer _fluidParticlePropertiesBuffer;
        private ComputeBuffer _fluidParticlePropertyPoolBuffer;

        private RenderTexture _fluidGridLevel0;
        private RenderTexture _fluidGridLevel1;
        private RenderTexture _fluidGridLevel2;

        private ComputeBuffer _fluidBlockParticleOffsetsBuffer;

        private ComputeBuffer _partitionSumsBuffer;

        private ComputeBuffer _indirectArgsBuffer;

        private ComputeBuffer _fluidEmitterDispatchBuffer;
        private ComputeBuffer _fluidEmitterPartitionBuffer;
        private ComputeBuffer _fluidEmitterPropertyBuffer;

#if UNITY_EDITOR
        public DebugBuffer DebugBuffer { get; private set; } = new DebugBuffer();
#endif

        public void LoadPhysicsScene(CommandBuffer cmd, APhysicsScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _renderPipeline = ARenderPipeline.Instance;
            _shaderSpecs = _renderPipeline.ShaderSpecs;

            #region allocate buffers/textures

            const int maxParticleCount = MaxParticleCount;
            _fluidParticlePositionsBuffer = new ComputeBuffer(4 + 2 * 4 * maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesBuffer = new ComputeBuffer(maxParticleCount * 8, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);
            _fluidParticlePropertyPoolBuffer = new ComputeBuffer(1 + maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _fluidGridLevel0 = CreateRWVolumeRT(GraphicsFormat.R32_SInt, GridSizeLevel0);
            _fluidGridLevel1 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, GridSizeLevel1);
            _fluidGridLevel2 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, GridSizeLevel2);

            _fluidBlockParticleOffsetsBuffer = new ComputeBuffer(8 + BlockCountLevel0 * 8,
                4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic);

            _partitionSumsBuffer = new ComputeBuffer(1 + 2 * PrefixSumPartitionCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _indirectArgsBuffer = new ComputeBuffer(3 * 3 + 4, 4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            unsafe
            {
                _fluidEmitterDispatchBuffer = new ComputeBuffer(MaxFluidEmitterDispatchCount, sizeof(FluidEmitterDispatch), ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
                _fluidEmitterPartitionBuffer = new ComputeBuffer(MaxFluidEmitterPartitionCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Dynamic);
                _fluidEmitterPropertyBuffer = new ComputeBuffer(MaxFluidEmitterPropertyCount, 4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
            }

            #endregion

            #region initialize buffers/textures

            // level 2 will be cleared at the beginning of every frame
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel0, 0);
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel1, 0);

            var parameters = new PhysicsSceneParameters(_renderPipeline.Scene, _physicsScene);
            ConstantBuffer.UpdateData(cmd, parameters);

            cmd.SetBufferData(_fluidParticlePositionsBuffer, new uint[] { 0, 0 });
            cmd.SetBufferData(_fluidParticlePropertyPoolBuffer, new uint[] { 0 });
            cmd.SetBufferData(_fluidBlockParticleOffsetsBuffer, new uint[] { 0, 1, 1, 0, 1, 1 });
            cmd.SetBufferData(_partitionSumsBuffer, new uint[] { 0 });
            cmd.SetBufferData(_indirectArgsBuffer, new uint[] { 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0 });

            cmd.SetGlobalBuffer(Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);

            ComputeShader shader = _shaderSpecs.FluidSolver.Shader;

            int kernel = _shaderSpecs.FluidSolver.ClearPartitionSumsKernel;
            int kernelSize = ClearPartitionSumsKernelSize;
            int groupCount = (PrefixSumPartitionCount + kernelSize - 1) / kernelSize;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.DispatchCompute(shader, kernel, groupCount, 1, 1);

            #endregion

            IsSceneLoaded = true;
        }

        public void UnloadPhysicsScene()
        {
            Debug.Assert(IsSceneLoaded);

            _shaderSpecs = null;
            _physicsScene = null;

            _fluidParticlePositionsBuffer.Release();
            _fluidParticlePropertiesBuffer.Release();
            _fluidParticlePropertyPoolBuffer.Release();
            _fluidBlockParticleOffsetsBuffer.Release();
            _partitionSumsBuffer.Release();
            _indirectArgsBuffer.Release();
            _fluidEmitterDispatchBuffer.Release();
            _fluidEmitterPartitionBuffer.Release();
            _fluidEmitterPropertyBuffer.Release();

            _fluidGridLevel0.Release();
            _fluidGridLevel1.Release();
            _fluidGridLevel2.Release();

            IsSceneLoaded = false;
        }

        public void Solve(CommandBuffer cmd, float deltaTime, int currentFrameAddParticleCount)
        {
#if UNITY_EDITOR
            DebugBuffer.Reset(cmd);
#endif

            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel2, 0);

            FluidSolverCompute fluidSolver = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidSolver.Shader;

            // set constant buffers
            {
                ConstantBuffer.Set<PhysicsSceneParameters>(cmd, shader, Bindings.PhysicsSceneParameters);

                var parameter = new PhysicsFrameParameters(_renderPipeline.Scene, _physicsScene, deltaTime, currentFrameAddParticleCount);
                ConstantBuffer.Push(cmd, parameter, shader, Bindings.PhysicsFrameParameters);
            }

            int kernel;

            kernel = fluidSolver.ClearFluidGridLevel0;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.ClearFluidGridLevel1;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 12);

            kernel = fluidSolver.GenerateIndirectArgs0Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertyPool, _fluidParticlePropertyPoolBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.ClearPartitionSumsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 24);

            kernel = fluidSolver.GenerateParticleHistogramKernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertyPool, _fluidParticlePropertyPoolBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.GenerateIndirectArgs1Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.GenerateParticleOffsetsKernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.SortParticlesKernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 12);

            kernel = fluidSolver.GenerateIndirectArgs2Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.ParticleToGrid0Kernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.ParticleToGrid1Kernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel0Kernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel1Kernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 12);

            kernel = fluidSolver.GridToParticleKernel;
#if UNITY_EDITOR
            DebugBuffer.SetParam(shader, kernel);
#endif
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);
        }

        public void AddParticles(CommandBuffer cmd, EmitterBufferBuilder emitterBufferBuilder)
        {
            Debug.Assert(emitterBufferBuilder.Submitted);

            if (emitterBufferBuilder.TotalParticleCount == 0)
                return;

            cmd.SetBufferData(_fluidEmitterDispatchBuffer, emitterBufferBuilder.EmitterDispatchBuffer);
            cmd.SetBufferData(_fluidEmitterPartitionBuffer, emitterBufferBuilder.PartitionBuffer);
            cmd.SetBufferData(_fluidEmitterPropertyBuffer, emitterBufferBuilder.PropertyBuffer);

            FluidSolverCompute fluidEmitter = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidEmitter.Shader;
            AddParticlesParameters parameters = new AddParticlesParameters(mass: 1f, randomSeed: Random.Range(0, int.MaxValue));
            ConstantBuffer.Push(cmd, parameters, shader, Bindings.AddParticlesParameters);

            int kernel = fluidEmitter.AddParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterDispatches, _fluidEmitterDispatchBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterPartitions, _fluidEmitterPartitionBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterProperties, _fluidEmitterPropertyBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertyPool, _fluidParticlePropertyPoolBuffer);
            cmd.DispatchCompute(shader, kernel, emitterBufferBuilder.PartitionCount, 1, 1);
        }

        public void RenderDebugParticles(CommandBuffer cmd, Camera camera, float particleSize = .25f)
        {
            DebugFluidParticleGraphics debugFluidParticle = _shaderSpecs.DebugFluidParticle;

            var parameters = new DebugFluidParticleGraphics.DebugFluidParticleParameters(
                camera.transform.position, camera.transform.up * particleSize);
            ConstantBuffer.PushGlobal(parameters, Bindings.DebugFluidParticleParameters);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, debugFluidParticle.Material, 0,
                MeshTopology.Points, _indirectArgsBuffer, 36);
        }
    }
}
