using System.Collections.Generic;
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

        private List<AShaderSpecifications.FluidSolverCompute.ParticleToAdd> _particlesToAdd;

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

                _particlesToAdd = new List<AShaderSpecifications.FluidSolverCompute.ParticleToAdd>();

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

                PhysicsPipeline.Solve(cmd, Time.fixedDeltaTime);
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
        public void AddTestParticles()
        {
            if (!enabled)
            {
                Debug.LogWarning($"enable {nameof(APhysicsScene)} before adding particles");
                return;
            }

            for (int i = 0; i < 64; i++)
            {
                Vector3 position = new Vector3(Random.value, Random.value, Random.value) * 10f;
                //Vector3 velocity = new Vector3(Random.value, Random.value, Random.value) - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 velocity = Vector3.zero;

                _particlesToAdd.Add(new AShaderSpecifications.FluidSolverCompute.ParticleToAdd(position, velocity));
            }
            //_particlesToAdd.Add(new AShaderSpecifications.FluidSolverCompute.ParticleToAdd(
            //    new Vector3(2.5f, 3.0f, 3.5f), new Vector3(1.0f, -1.0f, 0.0f)));
        }

#endif
    }
}
