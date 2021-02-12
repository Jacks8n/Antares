using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ShaderSpecification")]
    public partial class AShaderSpecs : ScriptableObject
    {
        private interface IShaderSpec
        {
            void OnAfterDeserialize();
        }

        public const int SceneMipCount = 5;
    }

    public partial class AShaderSpecs
    {
        [field: LabelText(nameof(ShaderSpecsInstance))]
        public static AShaderSpecs ShaderSpecsInstance { get; private set; }

        [field: SerializeField, LabelText(nameof(AtlasBlitCS))]
        public AtlasBlitCompute AtlasBlitCS { get; }

        [field: SerializeField, LabelText(nameof(SDFGenerationCS))]
        public SDFGenerationCompute SDFGenerationCS { get; }

        [field: SerializeField, LabelText(nameof(RayMarchingCS))]
        public RayMarchingCompute RayMarchingCS { get; }

        [field: SerializeField, LabelText(nameof(Deferred))]
        public DeferredGraphics Deferred { get; }

        private void Awake()
        {
            OnAfterDeserialize(AtlasBlitCS);
            OnAfterDeserialize(SDFGenerationCS);
            OnAfterDeserialize(RayMarchingCS);
            OnAfterDeserialize(Deferred);
        }

        private static void OnAfterDeserialize<T>(T shaderSpec) where T : IShaderSpec => shaderSpec.OnAfterDeserialize();
    }
}
