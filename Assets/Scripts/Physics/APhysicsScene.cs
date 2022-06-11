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

            PhysicsPipeline.UnloadPhysicsScene();
            _cmd.Dispose();
        }

        private void FixedUpdate()
        {
            if (PhysicsPipeline.IsSceneLoaded)
            {
                PhysicsPipeline.Solve(_cmd, Time.fixedDeltaTime);

                PhysicsFrameFence = _cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);

                UnityEngine.Graphics.ExecuteCommandBufferAsync(_cmd, ComputeQueueType.Default);
            }
        }
    }
}
