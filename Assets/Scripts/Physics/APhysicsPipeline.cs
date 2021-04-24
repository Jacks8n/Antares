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

        private ComputeBuffer _particlePositionsBuffer;

        private ComputeBuffer _particleFlagsBuffer;

        private ComputeBuffer _particleTracksBuffer;

        private float _deltaTimePrev;

        private int _particleCount;

        public void LoadPhysicsScene(SDFPhysicsScene scene)
        {
            Debug.Assert(!IsPhysicsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _cellVolume = CreateRWVolumeRT(GraphicsFormat.R32_UInt, scene.CellVolumeResolution, mipCount: 2);
            _cellLinkedListBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 2, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particlePositionsBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 4 * 3, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _particleFlagsBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount / 8, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            _particleTracksBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 2, ComputeBufferType.Default, ComputeBufferMode.Immutable);

            // initialize buffers
            {
                var fluidSolver = _shaderSpecs.FluidSolver;
                var sceneParam = new FluidSolverCompute.PhysicsSceneParameters(_physicsScene, _scene);
                fluidSolver.PhysicsSceneParamsCBSegment.UpdateCBuffer(_constantBuffer, sceneParam);
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
            _particlePositionsBuffer.Release();
            _particleFlagsBuffer.Release();
            _particleTracksBuffer.Release();

            IsPhysicsSceneLoaded = false;
        }

        public void AddParticles(List<Vector3> particles)
        {
            int packIndex = _particleCount / 64;
            int particleOffset = _particleCount % 64;
            int byteOffset = packIndex * 768 + particleOffset * 4;

            const int componentStride = sizeof(float) * 64;
            // todo
            var mapped = _particlePositionsBuffer.BeginWrite<float>(byteOffset,);

            _particleCount += particles.Count;
        }

        public void Solve(CommandBuffer cmd, float deltaTime)
        {
            var fluidSolver = _shaderSpecs.FluidSolver;
            {
                var param = new FluidSolverCompute.PhysicsFrameParameters(_physicsScene, deltaTime, _deltaTimePrev);
                fluidSolver.PhysicsFrameParamCBSegment.UpdateCBuffer(_constantBuffer, param);
            }

            ComputeShader shader = fluidSolver.Shader;
            int[] kernels = new int[] { fluidSolver.SetupCLLKernel, fluidSolver.SolveConstraintsKernel };
            for (int i = 0; i < 2; i++)
            {
                int kernel = kernels[i];
                cmd.SetComputeTextureParam(shader, kernel, ID_CellVolume, _cellVolume);
                cmd.SetComputeBufferParam(shader, kernel, ID_CellLinkedList, _cellLinkedListBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ID_ParticlePositions, _particlePositionsBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ID_ParticleFlags, _particleFlagsBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ID_ParticleTracks, _particleTracksBuffer);
                cmd.DispatchCompute(shader, kernel, );
            }

            _deltaTimePrev = deltaTime;
        }
    }
}
