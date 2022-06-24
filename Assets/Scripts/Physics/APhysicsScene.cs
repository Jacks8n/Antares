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

        [field: SerializeField, LabelText(nameof(Gravity))]
        public Vector3 Gravity { get; set; } = new Vector3(0f, 0f, -9.8f);

        [field: SerializeField, LabelText(nameof(GridSpacing))]
        public float GridSpacing { get; private set; } = 1f;

        private void OnEnable()
        {
            if (Instance)
                Instance.enabled = false;

            if (RenderPipelineManager.currentPipeline is ARenderPipeline renderPipeline)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                PhysicsPipeline = renderPipeline.GetPhysicsPipeline();
                PhysicsPipeline.LoadPhysicsScene(cmd, this);

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
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
        }

        private void FixedUpdate()
        {
            if (PhysicsPipeline != null && PhysicsPipeline.IsSceneLoaded)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                PhysicsPipeline.Solve(cmd, Time.fixedDeltaTime);

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

#if UNITY_EDITOR

        public void AddTestParticles(CommandBuffer cmd)
        {
            if (!enabled)
            {
                Debug.LogWarning($"enable {nameof(APhysicsScene)} before adding particles");
                return;
            }

            var particles = ListPool<AShaderSpecifications.FluidSolverCompute.ParticleToAdd>.Get();
            for (int i = 0; i < 64; i++)
            {
                Vector3 position = new Vector3(Random.value, Random.value, Random.value) * 10f;
                Vector3 velocity = new Vector3(Random.value, Random.value, Random.value) * 2f - Vector3.one;

                particles.Add(new AShaderSpecifications.FluidSolverCompute.ParticleToAdd(position, velocity));
            }

            PhysicsPipeline.AddParticles(cmd, particles);
            ListPool<AShaderSpecifications.FluidSolverCompute.ParticleToAdd>.Release(particles);
        }

        [Button]
        private void AddTestParticles()
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            AddTestParticles(cmd);
            UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }

#endif
    }
}
