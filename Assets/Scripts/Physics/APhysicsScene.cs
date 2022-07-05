using System.Collections.Generic;
using Antares.Graphics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.AShaderSpecifications.FluidSolverCompute;

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

        [field: SerializeField, LabelText(nameof(TimeScale))]
        public float TimeScale { get; private set; } = 1f;

        private List<ParticleToAdd> _particlesToAdd;

        private void OnEnable()
        {
            if (RenderPipelineManager.currentPipeline is ARenderPipeline renderPipeline)
            {
                {
                    CommandBuffer cmd = CommandBufferPool.Get();

                    PhysicsPipeline = renderPipeline.GetPhysicsPipeline();
                    PhysicsPipeline.LoadPhysicsScene(cmd, this);

                    UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                    CommandBufferPool.Release(cmd);
                }

                _particlesToAdd = new List<ParticleToAdd>();

                if (Instance)
                    Instance.enabled = false;

                Instance = this;
            }
            else
            {
                Debug.LogWarning($"current rendering pipeline is not {nameof(ARenderPipeline)}, " +
                    $"thus the physics pipeline is unavailable");

                enabled = false;
            }
        }

        private void OnDisable()
        {
            Instance = null;

            if (PhysicsPipeline != null)
                PhysicsPipeline.UnloadPhysicsScene();

            _particlesToAdd = null;
        }

        private void FixedUpdate()
        {
            if (PhysicsPipeline != null && PhysicsPipeline.IsSceneLoaded)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                PhysicsPipeline.Solve(cmd, Time.fixedDeltaTime * TimeScale);
                if (_particlesToAdd.Count > 0)
                {
                    PhysicsPipeline.AddParticles(cmd, _particlesToAdd);
                    _particlesToAdd.Clear();
                }

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

#if UNITY_EDITOR

        [Button]
        private void AddTestParticles()
        {
            if (!enabled)
            {
                Debug.LogWarning($"enable {nameof(APhysicsScene)} before adding particles");
                return;
            }

            AddTestParticles(_particlesToAdd);
        }

        public void AddTestParticles(List<ParticleToAdd> particles)
        {
            float positionRange = 10f;
            Vector3 positionOffset = new Vector3(0f, 0f, 0f);

            float velocityRange = 3f;
            for (int i = 0; i < 64; i++)
            {
                Vector3 position = positionRange * new Vector3(Random.value, Random.value, Random.value) + transform.position;
                position += positionOffset;

                Vector3 velocity = new Vector3(Random.value, Random.value, Random.value) - new Vector3(0.5f, 0.5f, 0.5f);
                velocity *= 2f * velocityRange;

                particles.Add(new ParticleToAdd(position, velocity));
            }
        }

#endif
    }
}
