using Sirenix.OdinInspector;
using Unity.Collections.LowLevel.Unsafe;
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

        public const float SDFSupremum = 4f;
    }

    public partial class AShaderSpecs
    {
        [field: LabelText(nameof(ShaderSpecsInstance))]
        public static AShaderSpecs ShaderSpecsInstance { get; private set; }

        public int ConstantBufferCount { get; private set; }

        // todo: compatibility
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

        private void OnEnable()
        {
            _constantBufferAlignment = checked(SystemInfo.constantBufferOffsetAlignment - 1);
            Debug.Assert((_constantBufferAlignment & (_constantBufferAlignment + 1)) == 0);
            Debug.Assert(ConstantBufferStride <= _constantBufferAlignment);

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

            ConstantBufferCount += sizeof(T);
            ConstantBufferCount = (ConstantBufferCount + _constantBufferAlignment) & ~_constantBufferAlignment;

            return offset / ConstantBufferStride;
        }
    }
}
