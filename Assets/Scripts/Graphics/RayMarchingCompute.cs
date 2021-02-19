using System;
using System.Runtime.InteropServices;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public class RayMarchingCompute : IShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFRayMarchingParameters
            {
                private readonly Vector4 UVToSceneRow0;
                private readonly Vector4 UVToSceneRow1;
                private readonly Vector4 UVToSceneRow2;

                private readonly Vector4 SceneTexel;

                private readonly Vector4 WorldToSceneTRow0;
                private readonly Vector4 WorldToSceneTRow1;
                private readonly Vector4 WorldToSceneTRow2;

                private readonly Vector4 SceneSize;

                private readonly Vector4 SDFSupremum;

                private readonly Vector3 TiledMarchingParams;

                public SDFRayMarchingParameters(Camera camera, SDFScene scene, float width, float height, float invW, float invH)
                {
                    float near = camera.nearClipPlane;
                    float dydvHalf = Mathf.Tan(Mathf.Deg2Rad * .5f * camera.fieldOfView) * near;
                    float dxduHalf = dydvHalf * camera.aspect;
                    Vector2 pixel = new Vector2(dxduHalf * invW, dydvHalf * invH);

                    Transform cameraTrans = camera.transform;
                    Matrix4x4 cameraToWorld = cameraTrans.localToWorldMatrix;
                    Vector3 right = (Vector3)cameraToWorld.GetColumn(0) * (2f * pixel.x);
                    right = scene.WorldToSceneVector(right);

                    Vector3 up = (Vector3)cameraToWorld.GetColumn(1) * (2f * pixel.y);
                    up = scene.WorldToSceneVector(up);

                    Vector3 lbn = cameraToWorld.MultiplyPoint(new Vector3(-dxduHalf, -dydvHalf, near));
                    lbn = scene.WorldToScenePoint(lbn);

                    Vector3 cameraPos = cameraTrans.position;
                    cameraPos = scene.WorldToScenePoint(cameraPos);

                    UVToSceneRow0 = new Vector4(right.x, up.x, lbn.x, cameraPos.x);
                    UVToSceneRow1 = new Vector4(right.y, up.y, lbn.y, cameraPos.y);
                    UVToSceneRow2 = new Vector4(right.z, up.z, lbn.z, cameraPos.z);

                    Vector3 texel = scene.SizeInv;
                    SceneTexel = new Vector4(texel.x, texel.y, texel.z, AShaderSpecs.SDFSupremum);

                    Transform sceneTrans = scene.transform;
                    Matrix4x4 worldToScene = sceneTrans.worldToLocalMatrix;
                    WorldToSceneTRow0 = worldToScene.GetRow(0);
                    WorldToSceneTRow1 = worldToScene.GetRow(1);
                    WorldToSceneTRow2 = worldToScene.GetRow(2);

                    Vector3 size = scene.Size;
                    SceneSize = new Vector4(size.x, size.y, size.z, 0f);

                    float supWorld = AShaderSpecs.SDFSupremum * scene.GridWorldSize;
                    SDFSupremum = new Vector4(
                        supWorld * (1 << (InitalSampleMip - 1)),
                        supWorld * (1 << (SceneMipCount - 1)),
                        supWorld,
                        InitalSampleMip);

                    TiledMarchingParams = new Vector3(width, height, pixel.magnitude) * .5f;
                }
            }

            public const int TiledMarchingGroupSizeX = 1;
            public const int TiledMarchingGroupSizeY = 1;
            public const int TiledMarchingGroupSizeZ = 1;

            public const int RayMarchingGroupSizeX = 8;
            public const int RayMarchingGroupSizeY = 8;
            public const int RayMarchingGroupSizeZ = 1;

            private const int InitalSampleMip = (SceneMipCount + 1) / 2;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int TiledMarchingKernel { get; private set; }

            public int RayMarchingKernel { get; private set; }

            public int RayMarchingParametersOffset { get; private set; }

            public unsafe int RayMarchingParametersSize => sizeof(SDFRayMarchingParameters);

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                TiledMarchingKernel = Shader.FindKernel("TiledMarching");
                RayMarchingKernel = Shader.FindKernel("RayMarching");

                RayMarchingParametersOffset = specs.RegisterConstantBuffer<SDFRayMarchingParameters>();
            }
        }
    }
}
