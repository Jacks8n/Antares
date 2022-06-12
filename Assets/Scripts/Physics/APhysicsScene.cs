using Antares.Graphics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Physics
{
    [ExecuteAlways]
    public class APhysicsScene : MonoBehaviour
    {
        public static APhysicsScene Instance { get; private set; }

        public APhysicsPipeline PhysicsPipeline { get; private set; }

        public GraphicsFence PhysicsFrameFence { get; private set; }

        [field: SerializeField, LabelText(nameof(Gravity))]
        public Vector3 Gravity { get; set; } = new Vector3(0f, 0f, -9.8f);

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(GridSpacing))]
        public float GridSpacing { get; private set; } = 1f;

        private CommandBuffer _cmd;

        private void OnEnable()
        {
            if (Instance)
                Instance.enabled = false;

            if (RenderPipelineManager.currentPipeline is ARenderPipeline renderPipeline)
            {
                _cmd = new CommandBuffer();

                PhysicsPipeline = renderPipeline.GetPhysicsPipeline();
                PhysicsPipeline.LoadPhysicsScene(_cmd, this);
            }
            else
                Debug.LogWarning($"current rendering pipeline is not {nameof(ARenderPipeline)}, " +
                    $"thus the physics pipeline is unavailable");

            Instance = this;
        }

        private void OnDisable()
        {
            Instance = null;

            if (PhysicsPipeline != null)
                PhysicsPipeline.UnloadPhysicsScene();
            _cmd.Dispose();
        }

        private void FixedUpdate()
        {
            if (PhysicsPipeline.IsSceneLoaded)
            {
                _cmd.Clear();

                PhysicsPipeline.Solve(_cmd, Time.fixedDeltaTime);

                PhysicsFrameFence = _cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);

                UnityEngine.Graphics.ExecuteCommandBufferAsync(_cmd, ComputeQueueType.Default);
            }
        }

#if UNITY_EDITOR

        [Button]
        private void AddParticles()
        {
            if (!enabled)
            {
                Debug.LogWarning($"enable {nameof(APhysicsScene)} before adding particles");
                return;
            }

            _cmd.Clear();

            var particles = ListPool<AShaderSpecifications.FluidSolverCompute.ParticleToAdd>.Get();
            for (int i = 0; i < 64; i++)
            {
                Vector3 position = new Vector3(Random.value, Random.value, Random.value) * 10f;
                Vector3 velocity = new Vector3(Random.value, Random.value, Random.value) * 2f - Vector3.one;

                particles.Add(new AShaderSpecifications.FluidSolverCompute.ParticleToAdd(position, velocity));
            }

            PhysicsPipeline.AddParticles(_cmd, particles);
            ListPool<AShaderSpecifications.FluidSolverCompute.ParticleToAdd>.Release(particles);

            UnityEngine.Graphics.ExecuteCommandBuffer(_cmd);
        }

#endif
    }
}
