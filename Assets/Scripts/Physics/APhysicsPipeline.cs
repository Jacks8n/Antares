using System.Collections.Generic;
using Antares.Graphics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.ARenderUtilities;
using static Antares.Graphics.AShaderSpecifications;

namespace Antares.Physics
{
    public class APhysicsPipeline
    {
        public bool IsSceneLoaded { get; private set; } = false;

        public int MaxAddParticleCount { get => 1024; }

        private APhysicsScene _physicsScene;

        private ARenderPipeline _renderPipeline;
        private AShaderSpecifications _shaderSpecs;

        private ComputeBuffer _fluidParticlePositionsBuffer;
        private ComputeBuffer _fluidParticlePropertiesBuffer;
        private ComputeBuffer _fluidParticlePropertiesPoolBuffer;

        private RenderTexture _fluidGridLevel0;
        private RenderTexture _fluidGridLevel1;
        private RenderTexture _fluidGridLevel2;

        private ComputeBuffer _fluidBlockParticleOffsetsBuffer;

        private ComputeBuffer _partitionSumsBuffer;

        private ComputeBuffer _indirectArgsBuffer;

        private ComputeBuffer _particlesToAddBuffer;

        public void LoadPhysicsScene(CommandBuffer cmd, APhysicsScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _renderPipeline = ARenderPipeline.Instance;
            _shaderSpecs = _renderPipeline.ShaderSpecs;

            #region allocate buffers/textures

            const int maxParticleCount = FluidSolverCompute.MaxParticleCount;
            _fluidParticlePositionsBuffer = new ComputeBuffer(4 + 2 * 4 * maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesBuffer = new ComputeBuffer(maxParticleCount * 8, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesPoolBuffer = new ComputeBuffer(1 + maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _fluidGridLevel0 = CreateRWVolumeRT(GraphicsFormat.R32_SInt, FluidSolverCompute.GridSizeLevel0);
            _fluidGridLevel1 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel1);
            _fluidGridLevel2 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel2);

            _fluidBlockParticleOffsetsBuffer = new ComputeBuffer(8 + FluidSolverCompute.BlockCountLevel0 * 8,
                4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic);

            _partitionSumsBuffer = new ComputeBuffer(1 + 2 * FluidSolverCompute.PrefixSumPartitionCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _indirectArgsBuffer = new ComputeBuffer(3 * 3 + 4, 4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            unsafe
            {
                _particlesToAddBuffer = new ComputeBuffer(MaxAddParticleCount * (sizeof(FluidSolverCompute.ParticleToAdd) / 4), 4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
            }

            #endregion

            #region initialize buffers/textures

            // level 2 will be cleared at the beginning of every frame
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel0, 0);
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel1, 0);

            var parameters = new FluidSolverCompute.PhysicsSceneParameters(_renderPipeline.Scene, _physicsScene);
            ConstantBuffer.UpdateData(cmd, parameters);

            cmd.SetBufferData(_fluidParticlePositionsBuffer, new uint[] { 0, 0 });
            cmd.SetBufferData(_fluidParticlePropertiesPoolBuffer, new uint[] { 0 });
            cmd.SetBufferData(_fluidBlockParticleOffsetsBuffer, new uint[] { 0, 1, 1, 0, 1, 1 });
            cmd.SetBufferData(_partitionSumsBuffer, new uint[] { 0 });
            cmd.SetBufferData(_indirectArgsBuffer, new uint[] { 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0 });

            cmd.SetGlobalBuffer(Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);

            ComputeShader shader = _shaderSpecs.FluidSolver.Shader;

            int kernel = _shaderSpecs.FluidSolver.ClearPartitionSumsKernel;
            int kernelSize = FluidSolverCompute.ClearPartitionSumsKernelSize;
            int groupCount = (FluidSolverCompute.PrefixSumPartitionCount + kernelSize - 1) / kernelSize;
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
            _fluidParticlePropertiesPoolBuffer.Release();
            _fluidBlockParticleOffsetsBuffer.Release();
            _partitionSumsBuffer.Release();
            _indirectArgsBuffer.Release();
            _particlesToAddBuffer.Release();

            _fluidGridLevel0.Release();
            _fluidGridLevel1.Release();
            _fluidGridLevel2.Release();

            IsSceneLoaded = false;
        }

        public void Solve(CommandBuffer cmd, float deltaTime, int currentFrameAddParticleCount)
        {
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel2, 0);

            FluidSolverCompute fluidSolver = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidSolver.Shader;

            // set constant buffers
            {
                ConstantBuffer.Set<FluidSolverCompute.PhysicsSceneParameters>(cmd, shader, Bindings.PhysicsSceneParameters);

                var parameter = new FluidSolverCompute.PhysicsFrameParameters(_renderPipeline.Scene, _physicsScene, deltaTime, currentFrameAddParticleCount);
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
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.ClearPartitionSumsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 24);

            kernel = fluidSolver.GenerateParticleHistogramKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.GenerateIndirectArgs1Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.GenerateParticleOffsetsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.SortParticlesKernel;
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
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.ParticleToGrid1Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel0Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel1Kernel;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 12);

            kernel = fluidSolver.GridToParticleKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);
        }

        public void AddParticles(CommandBuffer cmd, List<FluidSolverCompute.ParticleToAdd> particles, float mass = 1f)
        {
            Debug.Assert(particles.Count <= FluidSolverCompute.MaxParticleCount);

            FluidSolverCompute fluidSolver = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidSolver.Shader;

            {
                var parameters = new FluidSolverCompute.AddParticlesParameters((uint)particles.Count, mass);
                ConstantBuffer.Push(cmd, parameters, shader, Bindings.AddParticlesParameters);
            }

            cmd.SetBufferData(_particlesToAddBuffer, particles);

            int kernel = fluidSolver.AddParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.ParticlesToAdd, _particlesToAddBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);

            int groupCount = (particles.Count + FluidSolverCompute.AddParticlesKernelSize - 1) / FluidSolverCompute.AddParticlesKernelSize;
            cmd.DispatchCompute(shader, kernel, groupCount, 1, 1);
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
