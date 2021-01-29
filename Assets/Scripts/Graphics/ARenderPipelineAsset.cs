using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ARenderPipelineAsset")]
    public class ARenderPipelineAsset : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline() => new ARenderPipeline();
    }
}
