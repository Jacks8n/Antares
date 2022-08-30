using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ARenderPipelineAsset")]
    public class ARenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField, Required]
        private AShaderSpecifications _shaderSpecs;

        protected override RenderPipeline CreatePipeline()
        {
#if UNITY_EDITOR
            Debug.Log($"{nameof(ARenderPipeline)} is Created");
#endif

            return new ARenderPipeline(_shaderSpecs);
        }
    }
}
