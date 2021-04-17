using UnityEngine;
using Antares.Physics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.AShaderSpecs;

namespace Antares.Graphics
{
    public partial class ARenderPipeline
    {
        private SDFPhysicsScene _physicsScene;

        private RenderTexture _cellVolume;

        private ComputeBuffer _cellLinkedListBuffer;

        private ComputeBuffer _particlePositionsBuffer;

        private ComputeBuffer _particleFlagsBuffer;

        private ComputeBuffer _particleTracksBuffer;

        private float _deltaTimePrev;

        private partial bool LoadPhysicsScene(SDFPhysicsScene scene)
        {
            _physicsScene = scene;

            if (!_physicsScene)
                return false;

            _cellVolume = CreateRWVolumeRT(GraphicsFormat.R32_UInt, scene.CellVolumeResolution, mipCount: 2);
            _cellLinkedListBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 2, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particlePositionsBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 4 * 3, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particleFlagsBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount / 8, ComputeBufferType.Default, ComputeBufferMode.Immutable);
            _particleTracksBuffer = new ComputeBuffer(4, FluidSolverCompute.MaxParticleCount * 2, ComputeBufferType.Default, ComputeBufferMode.Immutable);

            // todo initialize buffers

            var fluidSolver = _shaderSpecs.FluidSolver;
            var sceneParam = new FluidSolverCompute.PhysicsSceneParameters(_physicsScene, _scene);
            fluidSolver.PhysicsSceneParamsCBSegment.UpdateCBuffer(_constantBuffer, sceneParam);
            fluidSolver.PhysicsSceneParamsCBSegment.BindCBuffer(fluidSolver.Shader, ID_PhysicsSceneParameters, _constantBuffer);

            fluidSolver.PhysicsFrameParamCBSegment.BindCBuffer(fluidSolver.Shader, ID_PhysicsFrameParameters, _constantBuffer);

            _deltaTimePrev = 0f;

            return true;
        }

        private partial void UnloadPhysicsScene()
        {
            _physicsScene = null;
            _cellVolume.Release();

            _cellVolume.Release();
            _cellLinkedListBuffer.Release();
            _particlePositionsBuffer.Release();
            _particleFlagsBuffer.Release();
            _particleTracksBuffer.Release();
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

        public void AddFluidParticle(Vector3 position)
        {
            // todo
        }
    }
}
