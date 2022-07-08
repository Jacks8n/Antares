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
#if UNITY_EDITOR
            if (_isCapturing)
                return;
#endif

            Iterate(Time.fixedDeltaTime * TimeScale);
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

        private void Iterate(float deltaTime)
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
                PhysicsPipeline.Solve(cmd, deltaTime, addParticleCount);

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

#if UNITY_EDITOR

        private enum TestSample { Cube }

        [SerializeField]
        [TitleGroup("Test Samples")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "<Pending>")]
        private TestSample _sampleType;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Size")]
        [ShowIf(nameof(_sampleType), TestSample.Cube)]
        private Vector3 _sampleParams_CubeSize;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Offset")]
        private Vector3 _sampleParams_Offset;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Velocity")]
        private Vector3 _sampleParams_Velocity;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Use Random Velocity")]
        private bool _sampleParams_RandomVelocity;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Particle Count")]
        [Range(1, 256)]
        private int _sampleParams_ParticleCount;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Capture Immediately after Adding Particales")]
        private bool _sampleParams_CaptureImmediately;

        [SerializeField]
        [TitleGroup("Test Samples")]
        [LabelText("Capture Count")]
        [Range(1, 8)]
        private int _sampleParams_CaptureCount;

        private bool _isCapturing;

        [TitleGroup("Test Samples")]
        [Button]
        private void AddTestParticles()
        {
            if (!enabled)
            {
                Debug.LogWarning($"Enable {nameof(APhysicsScene)} before Adding Particles.");
                return;
            }

            switch (_sampleType)
            {
                case TestSample.Cube:
                    AddParticlesCube(_sampleParams_CubeSize, _sampleParams_Offset, _sampleParams_Velocity,
                        _sampleParams_ParticleCount, _sampleParams_RandomVelocity);
                    break;
                default:
                    Debug.LogWarning("Unspported Sample Type.");
                    return;
            }

            if (_sampleParams_CaptureImmediately)
                CaptureFrame();
        }

        [Button]
        private void CaptureFrame()
        {
            if (RenderPipelineManager.currentPipeline is ARenderPipeline renderPipeline)
            {
                _isCapturing = true;
                renderPipeline.HookRenderDocCaptureEvents += () =>
                {
                    for (int i = 0; i < _sampleParams_CaptureCount; i++)
                        FixedUpdate();
                };
                _isCapturing = false;
            }
            else
                Debug.LogWarning($"Current Rendering Pipeline Is Not {nameof(ARenderPipeline)}.");
        }

#endif
    }
}
