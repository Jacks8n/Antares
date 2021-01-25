using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ARenderPipelineAsset")]
    public class ARenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField, InspectorName("SDF Generation"), Required]
        private ComputeShader _sdfGenerationCS;

        [SerializeField, InspectorName("Ray Marching"), Required]
        private ComputeShader _rayMarchingCS;

        [SerializeField, InspectorName("Shading"), Required]
        private Material _shadingMat;

        protected override RenderPipeline CreatePipeline()
        {
            return new ARenderPipeline(_sdfGenerationCS, _rayMarchingCS, _shadingMat);
        }
    }
}
