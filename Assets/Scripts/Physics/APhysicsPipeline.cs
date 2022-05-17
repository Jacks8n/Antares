using System.Collections.Generic;
using Antares.Physics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.AShaderSpecs;

namespace Antares.Graphics
{
    public partial class ARenderPipeline
    {
        public bool IsPhysicsSceneLoaded { get; private set; } = false;

        private SDFPhysicsScene _physicsScene;

        private RenderTexture _cellVolume;

        private ComputeBuffer _cellLinkedListBuffer;

        private ComputeBuffer _particlesBuffer;

        private ComputeBuffer _particleTracksBuffer;

        private ComputeBuffer _particlePoolBuffer;

        private ComputeBuffer _particleToCreateBuffer;

        private float _deltaTimePrev;

        private int _particleCount;

        public void LoadPhysicsScene(SDFPhysicsScene scene)
        {
            Debug.Assert(!IsPhysicsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _cellVolume = CreateRWVolumeRT(GraphicsFormat.R32_UInt, scene.CellVolumeResolution, mipCount: 2);

            const int maxParticleCount = FluidSolverCompute.MaxParticleCount;
            _cellLinkedListBuffer = new ComputeBuffer(maxParticleCount * 2, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particlesBuffer = new ComputeBuffer(maxParticleCount * 3, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particleTracksBuffer = new ComputeBuffer(maxParticleCount * 2, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);
            _particlePoolBuffer = new ComputeBuffer(maxParticleCount + 2, 4, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particleToCreateBuffer = new ComputeBuffer(FluidSolverCompute.MaxParticleCreateCount * 3, 4, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);

            // initialize buffers
            var fluidSolver = _shaderSpecs.FluidSolver;
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                var param = new FluidSolverCompute.PhysicsSceneParameters(_physicsScene, _scene);
                fluidSolver.PhysicsSceneParamsCBSegment.SetCBuffer(cmd, _constantBuffer, param);

                var shader = fluidSolver.Shader;
                int kernel = fluidSolver.FillParticlePoolKernel;
                {
                    cmd.SetBufferData(_particlePoolBuffer, new uint[] { maxParticleCount + 2, 0 });
                    cmd.SetComputeBufferParam(shader, kernel, ID_ParticlePool, _particlePoolBuffer);

                    cmd.DispatchCompute(fluidSolver.Shader, kernel, maxParticleCount / 64, 1, 1);
                }
            }

            _deltaTimePrev = 0f;
            _particleCount = 0;

            IsSceneLoaded = true;
        }

        public void UnloadPhysicsScene()
        {
            Debug.Assert(IsPhysicsSceneLoaded);

            _physicsScene = null;

            _cellVolume.Release();
            _cellLinkedListBuffer.Release();
            _particlesBuffer.Release();
            _particleTracksBuffer.Release();
            _particlePoolBuffer.Release();
            _particleToCreateBuffer.Release();

            IsPhysicsSceneLoaded = false;
        }

        public void AddParticles(CommandBuffer cmd, List<Vector3> particles)
        {

        }

        public void Solve(CommandBuffer cmd, float deltaTime)
        {
            var fluidSolver = _shaderSpecs.FluidSolver;
            {
                var param = new FluidSolverCompute.PhysicsFrameParameters(_physicsScene, deltaTime, _deltaTimePrev);
                fluidSolver.PhysicsFrameParamCBSegment.SubUpdateCBuffer(_constantBuffer, param);
            }

            ComputeShader shader = fluidSolver.Shader;
            int[] kernels = new int[] { fluidSolver.SetupCLLKernel, fluidSolver.SolveConstraintsKernel };
            for (int i = 0; i < 2; i++)
            {
                int kernel = kernels[i];
                cmd.SetComputeTextureParam(shader, kernel, ID_CellVolume, _cellVolume);
                cmd.SetComputeBufferParam(shader, kernel, ID_CellLinkedList, _cellLinkedListBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ID_Particles, _particlesBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ID_ParticleTracks, _particleTracksBuffer);
                //cmd.DispatchCompute(shader, kernel, );
            }

            _deltaTimePrev = deltaTime;
        }
    }
}
