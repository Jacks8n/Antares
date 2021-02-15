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

        public int ConstantBufferSize { get; private set; }

        [field: SerializeField, LabelText(nameof(AtlasBlitCS))]
        public AtlasBlitCompute AtlasBlitCS { get; private set; }

        [field: SerializeField, LabelText(nameof(SDFGenerationCS))]
        public SDFGenerationCompute SDFGenerationCS { get; private set; }

        [field: SerializeField, LabelText(nameof(RayMarchingCS))]
        public RayMarchingCompute RayMarchingCS { get; private set; }

        [field: SerializeField, LabelText(nameof(Deferred))]
        public DeferredGraphics Deferred { get; private set; }

        private int _constantBufferAlignment;

        private void Awake()
        {
            _constantBufferAlignment = SystemInfo.constantBufferOffsetAlignment;
            Debug.Assert((_constantBufferAlignment & (_constantBufferAlignment - 1)) == 0);

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
            return ConstantBufferSize = (ConstantBufferSize + _constantBufferAlignment - 1) & ~_constantBufferAlignment;
        }
    }
}
