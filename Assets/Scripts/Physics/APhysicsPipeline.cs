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

        private readonly AShaderSpecifications _shaderSpecs;

        private readonly ComputeBuffer _constantBuffer;

        private readonly MaterialPropertyBlock _debugFluidParticleMaterialProperties;

        private APhysicsScene _physicsScene;

        private ComputeBuffer _fluidParticlePositionsBuffer;
        private ComputeBuffer _fluidParticlePropertiesBuffer;

        private RenderTexture _fluidGridLevel0;
        private RenderTexture _fluidGridLevel1;
        private RenderTexture _fluidGridLevel2;

        private ComputeBuffer _fluidBlockParticleIndices;

        private ComputeBuffer _fluidGridAtomicLock;

        private ComputeBuffer _indirectArgsBuffer;

        private ComputeBuffer _particlesToAdd;

        public APhysicsPipeline(AShaderSpecifications shaderSpecs, ComputeBuffer constantBuffer)
        {
            _shaderSpecs = shaderSpecs;
            _constantBuffer = constantBuffer;

            _debugFluidParticleMaterialProperties = new MaterialPropertyBlock();
            _shaderSpecs.DebugFluidParticle.DebugFluidParticleParamsCBSpan.BindCBuffer(
                _debugFluidParticleMaterialProperties, Bindings.DebugFluidParticleParameters, _constantBuffer);
        }

        public void LoadPhysicsScene(CommandBuffer cmd, APhysicsScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            const int maxParticleCount = FluidSolverCompute.MaxParticleCount;
            _fluidParticlePositionsBuffer = new ComputeBuffer(6 + 2 * 4 * maxParticleCount, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesBuffer = new ComputeBuffer(maxParticleCount * 8, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);

            _fluidGridLevel0 = CreateRWVolumeRT(GraphicsFormat.R32_SInt, FluidSolverCompute.GridSizeLevel0);
            _fluidGridLevel1 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel1);
            _fluidGridLevel2 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, FluidSolverCompute.GridSizeLevel2);

            // level 2 will be cleared at the beginning of every frame
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel0, 0);
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel1, 0);

            _fluidBlockParticleIndices = new ComputeBuffer(6 + FluidSolverCompute.BlockCountLevel0 * FluidSolverCompute.BlockParticleStride,
                4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);

            _fluidGridAtomicLock = new ComputeBuffer(FluidSolverCompute.BlockCountLevel0 + FluidSolverCompute.BlockCountLevel1, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _shaderSpecs.TextureUtilCS.ClearBuffer(cmd, _fluidGridAtomicLock, 0);

            _indirectArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            _particlesToAdd = new ComputeBuffer(MaxAddParticleCount, 4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);

            // initialize buffers
            var fluidSolver = _shaderSpecs.FluidSolver;
            {
                var param = new FluidSolverCompute.PhysicsSceneParameters(_physicsScene);
                fluidSolver.PhysicsSceneParamsCBSpan.SetCBuffer(cmd, _constantBuffer, param);

                cmd.SetBufferData(_fluidParticlePositionsBuffer, new uint[] { 0, 0, 0 });
            }

            IsSceneLoaded = true;
        }

        public void UnloadPhysicsScene()
        {
            Debug.Assert(IsSceneLoaded);

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
            {
                var param = new FluidSolverCompute.PhysicsFrameParameters(_physicsScene, deltaTime);
                fluidSolver.PhysicsFrameParamsCBSpan.SetCBuffer(cmd, _constantBuffer, param);
            }

            ComputeShader shader = fluidSolver.Shader;
            fluidSolver.PhysicsSceneParamsCBSpan.BindCBuffer(shader, Bindings.PhysicsSceneParameters, _constantBuffer);
            fluidSolver.PhysicsFrameParamsCBSpan.BindCBuffer(shader, Bindings.PhysicsFrameParameters, _constantBuffer);

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

            {
                var param = new FluidSolverCompute.AddParticlesParameters((uint)particles.Count, mass);
                fluidSolver.AddParticlesParamsCBSpan.SetCBuffer(cmd, _constantBuffer, param);
            }

            cmd.SetBufferData(_particlesToAdd, particles);

            ComputeShader shader = fluidSolver.Shader;
            fluidSolver.AddParticlesParamsCBSpan.BindCBuffer(cmd, shader, Bindings.AddParticlesParameters, _constantBuffer);

            int kernel = fluidSolver.AddParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.ParticlesToAdd, _particlesToAdd);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);

            int groupCount = (particles.Count + FluidSolverCompute.AddParticlesKernelSize - 1) / FluidSolverCompute.AddParticlesKernelSize;
            cmd.DispatchCompute(shader, kernel, groupCount, 1, 1);
        }

        public void RenderDebugParticles(CommandBuffer cmd)
        {
            DebugFluidParticleGraphics debugFluidParticle = _shaderSpecs.DebugFluidParticle;

            cmd.DrawProceduralIndirect(Matrix4x4.identity, debugFluidParticle.Material, 0,
                MeshTopology.Points, _fluidParticlePositionsBuffer, 0, _debugFluidParticleMaterialProperties);
        }
    }
}
