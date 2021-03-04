using Antares.SDF;
using Antares.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ShaderSpecification")]
    public partial class AShaderSpecs : ScriptableObject, AShaderSpecs.IShaderAggregator
    {
        public struct ConstantBufferSegment<T> where T : unmanaged
        {
            public readonly int OffsetInBytes;

            public unsafe int Size => sizeof(T) < _constantBufferMimmumSize ? _constantBufferMimmumSize : sizeof(T);

            public ConstantBufferSegment(int offsetInBytes)
            {
                OffsetInBytes = offsetInBytes;
            }

            public void UpdateCBuffer(ComputeBuffer cbuffer, T data)
            {
                var mapped = cbuffer.BeginWrite<byte>(OffsetInBytes, Size);
                mapped.ReinterpretStore(0, data);
                cbuffer.EndWrite<byte>(Size);
            }

            public void BindCBuffer(CommandBuffer cmd, ComputeShader shader, int cbufferID, ComputeBuffer cbuffer)
            {
                cmd.SetComputeConstantBufferParam(shader, cbufferID, cbuffer, OffsetInBytes, Size);
            }

            public void BindCBuffer(ComputeShader shader, int cbufferID, ComputeBuffer cbuffer)
            {
                shader.SetConstantBuffer(cbufferID, cbuffer, OffsetInBytes, Size);
            }
        }

        private interface IShaderAggregator
        {
            ConstantBufferSegment<T> RegisterConstantBuffer<T>() where T : unmanaged;
        }

        private interface IShaderSpec
        {
            void OnAfterDeserialize<T>(T specs) where T : IShaderAggregator;
        }

        private interface IComputeShaderSpec : IShaderSpec
        {
            ComputeShader Shader { get; }
        }

        public const int SceneMipCount = 5;

        public const float SDFSupremum = 4f;
    }

    public partial class AShaderSpecs
    {
        [field: LabelText(nameof(ShaderSpecsInstance))]
        public static AShaderSpecs ShaderSpecsInstance { get; private set; }

        public int ConstantBufferCount { get; private set; }

        // todo: compatibility?
        public int ConstantBufferStride => 4;

        [field: SerializeField, LabelText(nameof(TextureUtilCS))]
        public TextureUtilCompute TextureUtilCS { get; private set; }

        [field: SerializeField, LabelText(nameof(SDFGenerationCS))]
        public SDFGenerationCompute SDFGenerationCS { get; private set; }

        [field: SerializeField, LabelText(nameof(RayMarchingCS))]
        public RayMarchingCompute RayMarchingCS { get; private set; }

        [field: SerializeField, LabelText(nameof(Deferred))]
        public DeferredGraphics Deferred { get; private set; }

        private int _constantBufferAlignment;

        private const int _constantBufferMimmumSize = 16;

        private void OnEnable()
        {
            _constantBufferAlignment = checked(SystemInfo.constantBufferOffsetAlignment - 1);
            Debug.Assert((_constantBufferAlignment & (_constantBufferAlignment + 1)) == 0);
            Debug.Assert(ConstantBufferStride <= _constantBufferAlignment);

            ConstantBufferCount = 0;

            InitializeSpec(TextureUtilCS);
            InitializeSpec(SDFGenerationCS);
            InitializeSpec(RayMarchingCS);
            InitializeSpec(Deferred);

            ConstantBufferCount = (ConstantBufferCount + ConstantBufferStride - 1) / ConstantBufferStride;

            ShaderSpecsInstance = this;
        }

        private void InitializeSpec<T>(T shaderSpec) where T : IShaderSpec
        {
            shaderSpec.OnAfterDeserialize(this);

#if UNITY_EDITOR
            if (shaderSpec is IComputeShaderSpec computeShaderSpec)
                ComputeShaderPostprocessor.SetImportHandler(computeShaderSpec.Shader, (_) =>
                {
                    OnEnable();
                    SDFScene.Instance.enabled = false;
                });
#endif
        }

        unsafe ConstantBufferSegment<T> IShaderAggregator.RegisterConstantBuffer<T>()
        {
            int offset = ConstantBufferCount;

            ConstantBufferCount += sizeof(T);
            ConstantBufferCount = (ConstantBufferCount + _constantBufferAlignment) & ~_constantBufferAlignment;

            Debug.Log($"cbuffer in size of {sizeof(T)} registered at {offset}");
            return new ConstantBufferSegment<T>(offset);
        }
    }
}
