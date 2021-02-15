using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ShaderSpecification")]
    public partial class AShaderSpecs : ScriptableObject, AShaderSpecs.IShaderAggregator
    {
        private interface IShaderAggregator
        {
            int RegisterConstantBuffer<T>() where T : unmanaged;
        }

        private interface IShaderSpec
        {
            void OnAfterDeserialize<T>(T specs) where T : IShaderAggregator;
        }

        public const int SceneMipCount = 5;
    }

    public partial class AShaderSpecs
    {
        [field: LabelText(nameof(ShaderSpecsInstance))]
        public static AShaderSpecs ShaderSpecsInstance { get; private set; }

        private static readonly int ConstantBufferAlignment;

        public int ConstantBufferSize { get; private set; }

        [field: SerializeField, LabelText(nameof(AtlasBlitCS))]
        public AtlasBlitCompute AtlasBlitCS { get; }

        [field: SerializeField, LabelText(nameof(SDFGenerationCS))]
        public SDFGenerationCompute SDFGenerationCS { get; }

        [field: SerializeField, LabelText(nameof(RayMarchingCS))]
        public RayMarchingCompute RayMarchingCS { get; }

        [field: SerializeField, LabelText(nameof(Deferred))]
        public DeferredGraphics Deferred { get; }

        static AShaderSpecs()
        {
            ConstantBufferAlignment = SystemInfo.constantBufferOffsetAlignment;
            Debug.Assert((ConstantBufferAlignment & (ConstantBufferAlignment - 1)) == 0);
        }

        private void Awake()
        {
            ConstantBufferSize = 0;

            InitializeSpec(AtlasBlitCS);
            InitializeSpec(SDFGenerationCS);
            InitializeSpec(RayMarchingCS);
            InitializeSpec(Deferred);
        }

        private void InitializeSpec<T>(T shaderSpec) where T : IShaderSpec => shaderSpec.OnAfterDeserialize(this);

        unsafe int IShaderAggregator.RegisterConstantBuffer<T>()
        {
            ConstantBufferSize += sizeof(T);
            return ConstantBufferSize = (ConstantBufferSize + ConstantBufferAlignment - 1) & ~ConstantBufferAlignment;
        }
    }
}
