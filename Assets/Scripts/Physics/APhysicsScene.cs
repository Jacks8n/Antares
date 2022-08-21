using System.Collections.Generic;
using System.Threading;
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

        private APhysicsPipeline.EmitterBufferBuilder _emitterBufferBuilder;

        private void Awake()
        {
            _emitterBufferBuilder = new APhysicsPipeline.EmitterBufferBuilder();
        }

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

                List<FluidEmitter.Particle> particleEmitters = FluidEmitterComponent<FluidEmitter.Particle>.GetEmitterInstanceList();
                List<FluidEmitter.Cube> cubeEmitters = FluidEmitterComponent<FluidEmitter.Cube>.GetEmitterInstanceList();

                _emitterBufferBuilder.AddEmitter(particleEmitters);
                _emitterBufferBuilder.AddEmitter(cubeEmitters);
                _emitterBufferBuilder.Submit();

                ClearEmitters(particleEmitters);
                ClearEmitters(cubeEmitters);

                PhysicsPipeline.AddParticles(cmd, _emitterBufferBuilder);
                PhysicsPipeline.Solve(cmd, deltaTime, _emitterBufferBuilder.TotalParticleCount);
                _emitterBufferBuilder.Clear();

                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

        private void ClearEmitters<T>(List<T> emitters) where T : IFluidEmitter
        {
            for (int i = 0; i < emitters.Count; i++)
                emitters[i].ClearParticles();
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
                {
                    if (Application.isPlaying)
                        Iterate(Time.fixedDeltaTime * TimeScale);
                    else
                        Iterate(1f / 60f * TimeScale);

                    Thread.Sleep(20);
                }

                _isCapturing = false;
            });
        }

#endif
    }
}
