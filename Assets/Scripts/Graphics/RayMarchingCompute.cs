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
                private readonly Vector3 UVToSceneColumn0;
                private readonly Vector3 UVToSceneColumn1;
                private readonly Vector3 UVToSceneColumn2;
                private readonly Vector3 UVToSceneColumn3;

                private readonly Vector4 SceneTexel;

                private readonly Vector3 WorldToSceneTColumn0;
                private readonly Vector3 WorldToSceneTColumn1;
                private readonly Vector3 WorldToSceneTColumn2;
                private readonly Vector3 WorldToSceneTColumn3;

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
                    Vector3 right = cameraTrans.localToWorldMatrix.GetColumn(0) * (2f * pixel.x);
                    UVToSceneColumn0 = scene.WorldToSceneVector(right);

                    Vector3 up = cameraTrans.localToWorldMatrix.GetColumn(1) * (2f * pixel.y);
                    UVToSceneColumn1 = scene.WorldToSceneVector(up);

                    Vector3 lbn = cameraTrans.localToWorldMatrix.MultiplyPoint(new Vector3(-dxduHalf, -dydvHalf, near));
                    UVToSceneColumn2 = scene.WorldToScenePoint(lbn);

                    Vector3 camPos = cameraTrans.position;
                    UVToSceneColumn3 = scene.WorldToScenePoint(camPos);

                    Vector3 texel = scene.SizeInv;
                    SceneTexel = new Vector4(texel.x, texel.y, texel.z, AShaderSpecs.SDFSupremum);

                    Transform sceneTrans = scene.transform;
                    Matrix4x4 worldToScene = sceneTrans.worldToLocalMatrix;
                    WorldToSceneTColumn0 = worldToScene.GetColumn(0);
                    WorldToSceneTColumn1 = worldToScene.GetColumn(1);
                    WorldToSceneTColumn2 = worldToScene.GetColumn(2);
                    WorldToSceneTColumn3 = worldToScene.GetColumn(3);

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

            public int ConstantBufferOffset { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                TiledMarchingKernel = Shader.FindKernel("TiledMarching");
                RayMarchingKernel = Shader.FindKernel("RayMarching");

                ConstantBufferOffset = specs.RegisterConstantBuffer<SDFRayMarchingParameters>();
            }
        }
    }
}
