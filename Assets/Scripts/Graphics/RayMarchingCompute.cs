using System;
using System.Runtime.InteropServices;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class RayMarchingCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct RayMarchingParameters
            {
                private readonly Matrix3x4 UVToScene;

                private readonly Vector3 SceneTexel;

                private readonly float RayMarchingParams;

                private readonly Vector4 SceneSize;

                private readonly Vector4 SDFBands;

                private readonly Vector4 TiledMarchingParams;

                public RayMarchingParameters(Camera camera, SDFScene scene, float invW, float invH)
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

                    UVToScene = new Matrix3x4(right, up, lbn, cameraPos);

                    Vector3 texel = scene.SizeInv;
                    SceneTexel = new Vector4(texel.x, texel.y, texel.z);

                    float pixelDiagHalfSqr = pixel.sqrMagnitude * .25f;
                    float pixelDiagHalf = Mathf.Sqrt(pixelDiagHalfSqr);
                    float pixelAperture = pixelDiagHalf / Mathf.Sqrt(pixelDiagHalfSqr + near * near);
                    RayMarchingParams = pixelAperture * 5f;

                    Vector3 size = scene.SizeInFloat;
                    SceneSize = new Vector4(size.x, size.y, size.z, SceneMipCount - 1);

                    float supWorld = scene.WorldSpaceSupremum;
                    SDFBands = new Vector4(
                        supWorld,
                        supWorld * (1 << (SceneMipCount - 1)),
                        supWorld * (1 << InitalSampleMip),
                        InitalSampleMip);

                    float tileDiagHalfSqr = pixelDiagHalfSqr * (MarchingTileSize * MarchingTileSize);
                    float tileDiagHalf = pixelDiagHalf * MarchingTileSize;
                    float tileAperture = tileDiagHalf / Mathf.Sqrt(tileDiagHalfSqr + near * near);
                    float sweepFactor = 1f / (1f + tileAperture);
                    float pauseThres = supWorld * .25f;
                    TiledMarchingParams = new Vector4(sweepFactor, 1f - sweepFactor, pauseThres, 0f);
                }
            }

            public const int TiledMarchingGroupSize = 8;

            public const int MarchingTileSize = 8;

            public const int RayMarchingGroupSize = MarchingTileSize;

            private const int InitalSampleMip = SceneMipCount / 2;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int TiledMarchingKernel { get; private set; }

            public int RayMarchingKernel { get; private set; }

            public int RayMarchingFluidKernel { get; private set; }

            public ConstantBufferSpan<RayMarchingParameters> RayMarchingParamsCBSpan { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                TiledMarchingKernel = Shader.FindKernel("TiledMarching");
                RayMarchingKernel = Shader.FindKernel("RayMarching");
                RayMarchingFluidKernel = Shader.FindKernel("RayMarchingFluid");

                RayMarchingParamsCBSpan = specs.RegisterConstantBuffer<RayMarchingParameters>();
            }
        }
    }
}
