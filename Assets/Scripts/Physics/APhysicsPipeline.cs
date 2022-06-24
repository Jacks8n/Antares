using System.Collections.Generic;
using Antares.Graphics;
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

        private AShaderSpecifications _shaderSpecs;

        private ComputeBuffer _fluidParticlePositionsBuffer;
        private ComputeBuffer _fluidParticlePropertiesBuffer;

        private RenderTexture _fluidGridLevel0;
        private RenderTexture _fluidGridLevel1;
        private RenderTexture _fluidGridLevel2;

        private ComputeBuffer _fluidBlockParticleIndices;

        private ComputeBuffer _fluidGridAtomicLock;

        private ComputeBuffer _indirectArgsBuffer;

        private ComputeBuffer _particlesToAdd;

        public void LoadPhysicsScene(CommandBuffer cmd, APhysicsScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _shaderSpecs = ARenderPipeline.Instance.ShaderSpecs;

            const int maxParticleCount = FluidSolverCompute.MaxParticleCount;
            _fluidParticlePositionsBuffer = new ComputeBuffer(6 + 2 * 4 * maxParticleCount, 4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesBuffer = new ComputeBuffer(maxParticleCount, 32, ComputeBufferType.Raw, ComputeBufferMode.Immutable);

            _fluidGridLevel0 = CreateRWVolumeRT(GraphicsFormat.R32_SInt, FluidSolverCompute.GridSizeLevel0);
            _fluidGridLevel1 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel1);
            _fluidGridLevel2 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel2);

            // level 2 will be cleared at the beginning of every frame
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel0, 0);
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel1, 0);

            _fluidBlockParticleIndices = new ComputeBuffer(6 + FluidSolverCompute.BlockCountLevel0 * FluidSolverCompute.BlockParticleStride,
                4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic);

            _fluidGridAtomicLock = new ComputeBuffer(FluidSolverCompute.BlockCountLevel0 + FluidSolverCompute.BlockCountLevel1, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            _shaderSpecs.TextureUtilCS.ClearBuffer(cmd, _fluidGridAtomicLock, 0);

            _indirectArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            unsafe
            {
                _particlesToAdd = new ComputeBuffer(MaxAddParticleCount * (sizeof(FluidSolverCompute.ParticleToAdd) / 4), 4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
            }

            // initialize buffers
            {
                var parameters = new FluidSolverCompute.PhysicsSceneParameters(_physicsScene);
                ConstantBuffer.UpdateData(cmd, parameters);

                cmd.SetBufferData(_fluidBlockParticleIndices, new uint[] { 0, 1, 1, 0, 1, 1 });
                cmd.SetBufferData(_fluidParticlePositionsBuffer, new uint[] { 1, 0, 0, 0, 1, 0 });
                cmd.SetGlobalBuffer(Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            }

            IsSceneLoaded = true;
        }

        public void UnloadPhysicsScene()
        {
            Debug.Assert(IsSceneLoaded);

            _shaderSpecs = null;
            _physicsScene = null;

            _fluidParticlePositionsBuffer.Release();
            _fluidParticlePropertiesBuffer.Release();
            _fluidBlockParticleIndices.Release();
            _fluidGridAtomicLock.Release();
            _indirectArgsBuffer.Release();
            _particlesToAdd.Release();

            _fluidGridLevel0.Release();
            _fluidGridLevel1.Release();
            _fluidGridLevel2.Release();

            IsSceneLoaded = false;
        }

        public void Solve(CommandBuffer cmd, float deltaTime)
        {
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel2, 0);

            FluidSolverCompute fluidSolver = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidSolver.Shader;

            // set constant buffers
            {
                ConstantBuffer.Set<FluidSolverCompute.PhysicsSceneParameters>(cmd, shader, Bindings.PhysicsSceneParameters);

                var parameter = new FluidSolverCompute.PhysicsFrameParameters(_physicsScene, deltaTime);
                ConstantBuffer.Push(cmd, parameter, shader, Bindings.PhysicsFrameParameters);
            }

            int kernel;

            kernel = fluidSolver.ClearFluidGridLevel0;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 0);

            kernel = fluidSolver.ClearFluidGridLevel1;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 12);

            // clear dispatches above depends on the indirect args left by last frame
            // per-block data is cleared when allocating blocks
            cmd.SetBufferData(_fluidBlockParticleIndices, new uint[] { 0, 1, 1, 0, 1, 1 });

            kernel = fluidSolver.GenerateIndirectArgsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.SortParticleKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleIndices, _fluidBlockParticleIndices);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidGridAtomicLock, _fluidGridAtomicLock);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.ParticleToGridKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleIndices, _fluidBlockParticleIndices);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 0);

            kernel = fluidSolver.SolveGridLevel0Kernel;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 0);

            kernel = fluidSolver.SolveGridLevel1Kernel;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 0);

            kernel = fluidSolver.GridToParticleKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleIndices, _fluidBlockParticleIndices);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleIndices, 0);
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

            cmd.SetBufferData(_particlesToAdd, particles);

            int kernel = fluidSolver.AddParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.ParticlesToAdd, _particlesToAdd);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);

            int groupCount = (particles.Count + FluidSolverCompute.AddParticlesKernelSize - 1) / FluidSolverCompute.AddParticlesKernelSize;
            cmd.DispatchCompute(shader, kernel, groupCount, 1, 1);
        }

        public void RenderDebugParticles(CommandBuffer cmd, Camera camera, float particleSize = .25f)
        {
            DebugFluidParticleGraphics debugFluidParticle = _shaderSpecs.DebugFluidParticle;

            var parameters = new DebugFluidParticleGraphics.DebugFluidParticleParameters(
                camera.transform.position, camera.transform.up * particleSize);
            ConstantBuffer.PushGlobal(cmd, parameters, Bindings.DebugFluidParticleParameters);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, debugFluidParticle.Material, 0,
                MeshTopology.Points, _fluidParticlePositionsBuffer, 0);
        }
    }
}
