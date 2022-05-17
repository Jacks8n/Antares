using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ARenderPipelineAsset")]
    public class ARenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField, Required]
        private AShaderSpecs _shaderSpecs;

        protected override RenderPipeline CreatePipeline() => new ARenderPipeline(_shaderSpecs);
    }
}
