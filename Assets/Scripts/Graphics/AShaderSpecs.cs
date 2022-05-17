using System.Runtime.InteropServices;
using Antares.SDF;
using Antares.Utility;
using Sirenix.OdinInspector;
using Unity.Collections;
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

            public void SubUpdateCBuffer(ComputeBuffer cbuffer, T data)
            {
                var mapped = cbuffer.BeginWrite<byte>(OffsetInBytes, Size);
                mapped.ReinterpretStore(0, data);
                cbuffer.EndWrite<byte>(Size);
            }

            public void SetCBuffer(CommandBuffer cmd, ComputeBuffer cbuffer, T data)
            {
                using var tmp = new NativeArray<T>(new T[] { data }, Allocator.Temp);
                cmd.SetBufferData(cbuffer, tmp.Reinterpret<byte>(), 0, OffsetInBytes, Size);
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

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct Matrix3x4
        {
            private readonly Vector4 Row0;
            private readonly Vector4 Row1;
            private readonly Vector4 Row2;

            public Matrix3x4(Vector4 row0, Vector4 row1, Vector4 row2)
            {
                Row0 = row0;
                Row1 = row1;
                Row2 = row2;
            }

            public Matrix3x4(Vector3 col0, Vector3 col1, Vector3 col2, Vector3 col3)
            {
                Row0 = new Vector4(col0.x, col1.x, col2.x, col3.x);
                Row1 = new Vector4(col0.y, col1.y, col2.y, col3.y);
                Row2 = new Vector4(col0.z, col1.z, col2.z, col3.z);
            }

            public Matrix3x4(Matrix4x4 matrix) : this(matrix.GetRow(0), matrix.GetRow(1), matrix.GetRow(2)) { }

            public static implicit operator Matrix3x4(Matrix4x4 matrix) => new Matrix3x4(matrix);
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

        [field: SerializeField, LabelText(nameof(FluidSolver))]
        public FluidSolverCompute FluidSolver { get; private set; }

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
