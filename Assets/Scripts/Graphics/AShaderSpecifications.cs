using System.Runtime.InteropServices;
using Antares.SDF;
using Antares.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ShaderSpecification")]
    public partial class AShaderSpecifications : ScriptableObject
    {
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

        private interface IShaderSpec
        {
            void Initialize();
        }

        private interface IComputeShaderSpec : IShaderSpec
        {
            ComputeShader Shader { get; }
        }

        public const int SceneMipCount = 5;

        public const float SDFSupremum = 4f;

        public static AShaderSpecifications Instance { get; private set; }

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

        [field: SerializeField, LabelText(nameof(DebugFluidParticle))]
        public DebugFluidParticleGraphics DebugFluidParticle { get; private set; }

#if UNITY_EDITOR
        private int _initializedShaderCount;
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            Debug.Log($"Constant Buffer Alignment: {SystemInfo.constantBufferOffsetAlignment}");
            Debug.Log($"Wave Lane Count: {SystemInfo.computeSubGroupSize}");

            _initializedShaderCount = 0;
#endif

            InitializeSpec(TextureUtilCS);
            InitializeSpec(SDFGenerationCS);
            InitializeSpec(RayMarchingCS);
            InitializeSpec(Deferred);
            InitializeSpec(FluidSolver);
            InitializeSpec(DebugFluidParticle);

            Instance = this;

#if UNITY_EDITOR
            CheckInitialization();
#endif
        }

        private void InitializeSpec<T>(T shaderSpec) where T : IShaderSpec
        {
            shaderSpec.Initialize();

#if UNITY_EDITOR
            if (shaderSpec is IComputeShaderSpec computeShaderSpec)
                ComputeShaderPostprocessor.SetImportHandler(computeShaderSpec.Shader, (_) =>
                {
                    OnEnable();
                    SDFScene.Instance.enabled = false;
                });

            _initializedShaderCount++;
#endif
        }

#if UNITY_EDITOR
        private void CheckInitialization()
        {
            var shaderType = typeof(IShaderSpec);

            int totalShaderCount = 0;
            var properties = typeof(AShaderSpecifications).GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var type = property.PropertyType;

                if (shaderType.IsAssignableFrom(type))
                    totalShaderCount++;
            }

            Debug.Assert(totalShaderCount == _initializedShaderCount,
                $"Total shader count: {totalShaderCount}. Actually initialized shader count: {_initializedShaderCount}.");
        }
#endif
    }
}
