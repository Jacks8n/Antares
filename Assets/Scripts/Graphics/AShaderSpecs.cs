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

        public int ConstantBufferCount { get; private set; }

        public int ConstantBufferStride => 4;

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
            _constantBufferAlignment = checked(SystemInfo.constantBufferOffsetAlignment - 1);
            Debug.Assert((_constantBufferAlignment & (_constantBufferAlignment + 1)) == 0);

            ConstantBufferCount = 0;

            InitializeSpec(AtlasBlitCS);
            InitializeSpec(SDFGenerationCS);
            InitializeSpec(RayMarchingCS);
            InitializeSpec(Deferred);

            ConstantBufferCount = (ConstantBufferCount + ConstantBufferStride - 1) / ConstantBufferStride;
        }

        private void InitializeSpec<T>(T shaderSpec) where T : IShaderSpec => shaderSpec.OnAfterDeserialize(this);

        unsafe int IShaderAggregator.RegisterConstantBuffer<T>()
        {
            int offset = ConstantBufferCount;

            ConstantBufferCount += (sizeof(T) + ConstantBufferStride - 1) / ~(ConstantBufferStride - 1);
            ConstantBufferCount = (ConstantBufferCount + _constantBufferAlignment) & ~_constantBufferAlignment;

            return offset;
        }
    }
}
