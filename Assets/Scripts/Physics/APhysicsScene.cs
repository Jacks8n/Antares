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
                Debug.LogWarning($"Current Rendering Pipeline Is Not {nameof(ARenderPipeline)}, " +
                    $"Physics Pipeline Is Unavailable.");

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

                int addParticleCount = _particlesToAdd.Count;
                if (addParticleCount > 0)
                {
                    PhysicsPipeline.AddParticles(cmd, _particlesToAdd);
                    _particlesToAdd.Clear();
                }
                PhysicsPipeline.Solve(cmd, Time.fixedDeltaTime * TimeScale, addParticleCount);

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

        public void AddParticles(List<ParticleToAdd> particles)
        {
            for (int i = 0; i < particles.Count; i++)
                _particlesToAdd.Add(particles[i]);
        }

        public void AddParticlesCube(Vector3 size, Vector3 offset, Vector3 velocity, int particleCount, bool useRandomVelocity = false)
        {
            for (int i = 0; i < particleCount; i++)
            {
                Vector3 particlePosition = new Vector3(Random.value * size.x, Random.value * size.y, Random.value * size.z);
                particlePosition += offset;

                Vector3 particleVelocity = velocity;
                if (useRandomVelocity)
                {
                    particleVelocity.x *= Random.value;
                    particleVelocity.y *= Random.value;
                    particleVelocity.z *= Random.value;
                }

                _particlesToAdd.Add(new ParticleToAdd(particlePosition, particleVelocity));
            }
        }

#if UNITY_EDITOR

        private enum TestSample { Cube }

        [SerializeField]
        [TitleGroup("Test Samples")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "<Pending>")]
        private TestSample SampleType;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Size")]
        [ShowIf(nameof(SampleType), TestSample.Cube)]
        private Vector3 SampleParams_CubeSize;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Offset")]
        private Vector3 SampleParams_Offset;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Velocity")]
        private Vector3 SampleParams_Velocity;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Use Random Velocity")]
        private bool SampleParams_RandomVelocity;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Particle Count")]
        [Range(1, 128)]
        private int SampleParams_ParticleCount;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Capture Immediately after Adding Particales")]
        private bool SampleParams_CaptureImmediately;

        [TitleGroup("Test Samples")]
        [Button]
        private void AddTestParticles()
        {
            if (!enabled)
            {
                Debug.LogWarning($"Enable {nameof(APhysicsScene)} before Adding Particles.");
                return;
            }

            switch (SampleType)
            {
                case TestSample.Cube:
                    AddParticlesCube(SampleParams_CubeSize, SampleParams_Offset, SampleParams_Velocity,
                        SampleParams_ParticleCount, SampleParams_RandomVelocity);
                    break;
                default:
                    Debug.LogWarning("Unspported Sample Type.");
                    return;
            }

            if (SampleParams_CaptureImmediately)
                CaptureFrame();
        }

        [Button]
        private void CaptureFrame()
        {
            if (RenderPipelineManager.currentPipeline is ARenderPipeline renderPipeline)
            {
                renderPipeline.HookRenderDocCaptureEvents += () => { FixedUpdate(); };
            }
            else
                Debug.LogWarning($"Current Rendering Pipeline Is Not {nameof(ARenderPipeline)}.");
        }

#endif
    }
}
