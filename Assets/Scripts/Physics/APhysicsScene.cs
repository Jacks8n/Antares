using System;
using System.Collections.Generic;
using Antares.Graphics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

using UGraphics = UnityEngine.Graphics;

namespace Antares.Physics
{
    [ExecuteAlways]
    public class APhysicsScene : MonoBehaviour
    {
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

                    UGraphics.ExecuteCommandBuffer(cmd);

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

#if UNITY_EDITOR
                float emitterDeltaTime = _debugging_DisableEmitters ? 0f : deltaTime;
#else
                float emitterDeltaTime = deltaTime;
#endif

                var particleEmitters = FluidEmitterComponent<FluidEmitter.Particle>.GetEmitterInstanceList(emitterDeltaTime);
                var cubeEmitters = FluidEmitterComponent<FluidEmitter.Cube>.GetEmitterInstanceList(emitterDeltaTime);

#if UNITY_EDITOR
                if (!_debugging_DisableEmitters)
#endif
                {
                    _emitterBufferBuilder.AddEmitter(particleEmitters);
                    _emitterBufferBuilder.AddEmitter(cubeEmitters);
                }

                _emitterBufferBuilder.Submit();

                ClearEmitters(particleEmitters);
                ClearEmitters(cubeEmitters);

                PhysicsPipeline.AddParticles(cmd, _emitterBufferBuilder);
                PhysicsPipeline.Solve(cmd, deltaTime, _emitterBufferBuilder.TotalParticleCount);
                _emitterBufferBuilder.Clear();

                UGraphics.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }
        }

        private void ClearEmitters<T>(List<T> emitters) where T : IFluidEmitter
        {
            for (int i = 0; i < emitters.Count; i++)
                emitters[i].ClearParticles();
        }

#if UNITY_EDITOR

        [TitleGroup("Debugging")]
        [Range(1, 8)]
        [ShowInInspector]
        [LabelText("Capture Count")]
        private int _debugging_CaptureCount;

        [TitleGroup("Debugging")]
        [ShowInInspector]
        [LabelText("Disable Emitters")]
        private bool _debugging_DisableEmitters;

        private volatile bool _isCapturing;

        [TitleGroup("Debugging")]
        [Button]
        private void CaptureFrame()
        {
            if (!enabled)
            {
                Debug.LogWarning($"Enable {nameof(APhysicsScene)} before Capturing");
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("Capturing Physics Frame is Only Available in Play Mode");
                return;
            }

            ARenderPipeline.AddCaptureEvent(() =>
            {
                _isCapturing = true;

                for (int i = 0; i < _debugging_CaptureCount; i++)
                    Iterate(Time.fixedDeltaTime * TimeScale);

                _isCapturing = false;
            });
        }

        [TitleGroup("Debugging")]
        [Button("Enable Emitters and Capture Immediately")]
        private void CaptureEmitters()
        {
            _debugging_DisableEmitters = false;
            CaptureFrame();
        }

#endif
    }
}
