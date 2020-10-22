using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ARenderPipelineAsset")]
    public class ARenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField, InspectorName("Ray Marching")]
        private Material _rayMarchingMat;

        protected override RenderPipeline CreatePipeline()
        {
            Debug.Assert(_rayMarchingMat);

            Resolution resolution = Screen.currentResolution;
            return new ARenderPipeline(resolution.width, resolution.height, _rayMarchingMat);
        }
    }
}
