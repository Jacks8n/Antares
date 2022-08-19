using System.Collections.Generic;
using Antares.Graphics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Physics
{
    [ExecuteAlways]
    [DefaultExecutionOrder(ExecutionOrder)]
    public class APhysicsScene : MonoBehaviour
    {
        public const int ExecutionOrder = 1000;

        public static APhysicsScene Instance { get; private set; }

        public APhysicsPipeline PhysicsPipeline { get; private set; }

        [field: SerializeField, LabelText(nameof(Gravity))]
        public Vector3 Gravity { get; set; } = new Vector3(0f, 0f, -9.8f);

        [field: SerializeField, LabelText(nameof(GridSpacing)), HideIf("enabled")]
        public float GridSpacing { get; private set; } = 1f;

        [field: SerializeField, LabelText(nameof(TimeScale))]
        public float TimeScale { get; private set; } = 1f;

        public FluidEmitter.Particle ParticleFluidEmitter { get; private set; }

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

                ParticleFluidEmitter = new FluidEmitter.Particle();

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

            ParticleFluidEmitter = null;
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (_isCapturing)
                return;
#endif

            Iterate(Time.fixedDeltaTime * TimeScale);
        }

        private void Iterate(float deltaTime)
        {
            if (PhysicsPipeline != null && PhysicsPipeline.IsSceneLoaded)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                List<FluidEmitter.Particle> particleEmitters = FluidEmitterComponent<FluidEmitter.Particle>.GetEmitterInstances();
                List<FluidEmitter.Cube> cubeEmitters = FluidEmitterComponent<FluidEmitter.Cube>.GetEmitterInstances();
                using (var builder = new APhysicsPipeline.EmitterBufferBuilder())
                {
                    builder.Reserve(particleEmitters);
                    builder.Reserve(cubeEmitters);

                    builder.Allocate();

                    builder.AddEmitter(particleEmitters);
                    builder.AddEmitter(cubeEmitters);

                    PhysicsPipeline.AddParticles(cmd, builder);
                    PhysicsPipeline.Solve(cmd, deltaTime, builder.TotalParticleCount);
                }

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

#if UNITY_EDITOR

        [SerializeField]
        [TitleGroup("Debugging")]
        [LabelText("Capture Count")]
        [Range(1, 8)]
        private int _sampleParams_CaptureCount;

        private bool _isCapturing;

        [TitleGroup("Debugging")]
        [Button]
        private void CaptureFrame()
        {
            if (!enabled)
            {
                Debug.LogWarning($"Enable {nameof(APhysicsScene)} before Capturing");
                return;
            }

            ARenderPipeline.AddCaptureEvent(() =>
            {
                _isCapturing = true;

                for (int i = 0; i < _sampleParams_CaptureCount; i++)
                    if (Application.isPlaying)
                        Iterate(Time.fixedDeltaTime * TimeScale);
                    else
                        Iterate(1f / 60f * TimeScale);

                _isCapturing = false;
            });
        }

#endif
    }
}
