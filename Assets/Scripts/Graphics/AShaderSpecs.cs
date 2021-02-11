using System;
using System.Runtime.InteropServices;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [CreateAssetMenu(menuName = "Rendering/ShaderSpecification")]
    public partial class AShaderSpecs : ScriptableObject
    {
        [Serializable]
        public class AtlasBlitCompute
        {
            public const int BlitMipGroupSizeX = 8;
            public const int BlitMipGroupSizeY = 8;
            public const int BlitMipGroupSizeZ = 8;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; }

            public int BlitKernel { get; private set; }

            public void OnAfterDeserialize() => BlitKernel = Shader.FindKernel("Blit");
        }

        [Serializable]
        public partial class SDFGenerationCompute
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFBrush
            {
                private readonly Vector3 WorldToLocalCol0;
                private readonly Vector3 WorldToLocalCol1;
                private readonly Vector3 WorldToLocalCol2;
                private readonly Vector3 WorldToLocalCol3;

                private readonly uint BrushType;

                private readonly uint MaterialID;

                private readonly uint ParameterOffset;

                private readonly float Scale;

                public SDFBrush(SDFBrushProperty brushProperty, uint parameterOffset)
                {
                    Matrix4x4 worldToLocal = brushProperty.Transform.WorldToLocal;
                    WorldToLocalCol0 = worldToLocal.GetColumn(0);
                    WorldToLocalCol1 = worldToLocal.GetColumn(1);
                    WorldToLocalCol2 = worldToLocal.GetColumn(2);
                    WorldToLocalCol3 = worldToLocal.GetColumn(3);

                    BrushType = (uint)brushProperty.BrushType;
                    MaterialID = brushProperty.MaterialID;
                    ParameterOffset = parameterOffset;
                    Scale = brushProperty.Transform.Scale;
                }
            }

            public const int CalculateMipGroupSizeX = 4;
            public const int CalculateMipGroupSizeY = 4;
            public const int CalculateMipGroupSizeZ = 4;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; }

            public int GenerateMatVolumeKernel { get; private set; }

            public int GenerateMipDispatchKernel { get; private set; }

            public int GenerateSceneVolumeKernel { get; private set; }

            public int GenerateMipMapKernel { get; private set; }

            public void OnAfterDeserialize()
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");
            }
        }

        [Serializable]
        public class RayMarchingCompute
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFRayMarchingParameters
            {
                private readonly Vector3 UVToSceneColumn0;
                private readonly Vector3 UVToSceneColumn1;
                private readonly Vector3 UVToSceneColumn2;
                private readonly Vector3 UVToSceneColumn3;

                private readonly Vector4 SceneTexel;

                private readonly Vector4 SceneSize;

                private readonly Vector3 WorldToSceneTColumn0;
                private readonly Vector3 WorldToSceneTColumn1;
                private readonly Vector3 WorldToSceneTColumn2;
                private readonly Vector3 WorldToSceneTColumn3;

                public SDFRayMarchingParameters(Camera camera, SDFScene scene, float invW, float invH)
                {
                    float near = camera.nearClipPlane;
                    float dydvHalf = Mathf.Tan((Mathf.Deg2Rad * .5f) * camera.fieldOfView) * near;
                    float dxduHalf = dydvHalf * camera.aspect;

                    Transform cameraTrans = camera.transform;
                    Vector3 right = cameraTrans.localToWorldMatrix.GetColumn(0) * (2f * invW * dxduHalf);
                    UVToSceneColumn0 = scene.WorldToSceneVector(right);

                    Vector3 up = cameraTrans.localToWorldMatrix.GetColumn(1) * (2f * invH * dydvHalf);
                    UVToSceneColumn1 = scene.WorldToSceneVector(up);

                    Vector3 lbn = cameraTrans.localToWorldMatrix.MultiplyPoint(new Vector3(-dxduHalf, -dydvHalf, near));
                    UVToSceneColumn2 = scene.WorldToScenePoint(lbn);

                    Vector3 camPos = cameraTrans.position;
                    UVToSceneColumn3 = scene.WorldToScenePoint(camPos);

                    Vector3 texel = scene.SizeInv;
                    SceneTexel = new Vector4(texel.x, texel.y, texel.z, SDFGenerator.MaxDistance);

                    Vector3 size = scene.Size;
                    SceneSize = new Vector4(size.x, size.y, size.z, 0f);

                    Transform sceneTrans = scene.transform;
                    Matrix4x4 worldToScene = sceneTrans.worldToLocalMatrix;
                    WorldToSceneTColumn0 = worldToScene.GetColumn(0);
                    WorldToSceneTColumn1 = worldToScene.GetColumn(1);
                    WorldToSceneTColumn2 = worldToScene.GetColumn(2);
                    WorldToSceneTColumn3 = worldToScene.GetColumn(3);
                }
            }

            public const int TiledMarchingGroupSizeX = 1;
            public const int TiledMarchingGroupSizeY = 1;
            public const int TiledMarchingGroupSizeZ = 1;

            public const int RayMarchingGroupSizeX = 8;
            public const int RayMarchingGroupSizeY = 8;
            public const int RayMarchingGroupSizeZ = 1;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; }

            public int TiledMarchingKernel { get; private set; }

            public int RayMarchingKernel { get; private set; }

            public void OnAfterDeserialize()
            {
                TiledMarchingKernel = Shader.FindKernel("TiledMarching");
                RayMarchingKernel = Shader.FindKernel("RayMarching");
            }
        }

        [Serializable]
        public class DeferredGraphics
        {
            [field: SerializeField, LabelText(nameof(Shader))]
            public Shader Shader { get; }

            public Material Material { get; private set; }

            public void OnAfterDeserialize() => Material = new Material(Shader);
        }

        public const int SceneMipCount = 5;

        public const int MaterialVolumeScale = 4;

        /// <summary>
        /// Any dimension of scene volume should be multiple of this number.
        /// </summary>
        public const int SceneSizeAlignment = (1 << (SceneMipCount - 1)) * MaterialVolumeScale;
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
            AtlasBlitCS.OnAfterDeserialize();
            SDFGenerationCS.OnAfterDeserialize();
            RayMarchingCS.OnAfterDeserialize();
            Deferred.OnAfterDeserialize();
        }
    }
}
