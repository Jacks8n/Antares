using Sirenix.OdinInspector;
using Antares.Graphics;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector.Editor;

namespace Antares.SDF
{
    [ExecuteAlways]
    public class SDFScene : MonoBehaviour
    {
        public const int SceneMipCount = 5;

        public static SDFScene Instance { get; private set; }

        [field: SerializeField, LabelText("Scene Resolution")]
        public Vector3Int Size { get; private set; }

        public Vector3 SizeInv { get; private set; }

        [field: SerializeField, Required]
        public SDFBrushCollection Brushes { get; }

        public RenderTargetIdentifier Volume => _volume;

        private RenderTexture _volume;

        public Vector3 WorldToSceneVector(Vector3 vec) => transform.worldToLocalMatrix.MultiplyVector(vec);

        public Vector3 WorldToScenePoint(Vector3 pos) => transform.worldToLocalMatrix.MultiplyPoint(pos);

        private void Awake()
        {
            RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor() {
                dimension = TextureDimension.Tex3D,
                width = Size.x,
                height = Size.z,
                volumeDepth = Size.y,
                colorFormat = RenderTextureFormat.R8,
                msaaSamples = 1,
                mipCount = SceneMipCount,
                autoGenerateMips = false,
                enableRandomWrite = true
            };
            _volume = new RenderTexture(volumeDesc);
            SizeInv = new Vector3(1f / Size.x, 1f / Size.y, 1f / Size.z);
        }

        private void OnEnable()
        {
            if (Instance && Instance != this)
                Instance.enabled = false;
            Instance = this;

            _volume.Create();

            TryGetRenderPipeline()?.LoadScene(this);
        }

        private void OnDisable()
        {
            Instance._volume.Release();
        }

        private void Update()
        {
#if UNITY_EDITOR
            Vector3 scale = transform.localScale;
            if (scale.x != scale.y || scale.y != scale.z || scale.z != scale.x)
            {
                Debug.LogWarning("Only the affine transfrom is permitted");
                transform.localScale = new Vector3(scale.x, scale.x, scale.x);
            }
#endif
        }

        private static ARenderPipeline TryGetRenderPipeline()
        {
            RenderPipeline pipeline = RenderPipelineManager.currentPipeline;
            if (pipeline is ARenderPipeline)
                return pipeline as ARenderPipeline;
            return null;
        }
    }
}
