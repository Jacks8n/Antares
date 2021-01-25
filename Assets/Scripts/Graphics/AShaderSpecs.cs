using System.Runtime.InteropServices;
using Antares.SDF;
using UnityEngine;

namespace Antares.Graphics
{
    public static class AShaderSpecs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SDFRayMarchingParameters
        {
            public Vector3 UVToSceneColumn0;
            public Vector3 UVToSceneColumn1;
            public Vector3 UVToSceneColumn2;
            public Vector3 UVToSceneColumn3;

            public Vector4 SceneTexel;

            public Vector4 SceneSize;

            public Vector3 WorldToSceneTColumn0;
            public Vector3 WorldToSceneTColumn1;
            public Vector3 WorldToSceneTColumn2;
            public Vector3 WorldToSceneTColumn3;

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

        public const int SceneMipCount = 4;

        public const int CalculateMipGroupSizeX = 4;
        public const int CalculateMipGroupSizeY = 4;
        public const int CalculateMipGroupSizeZ = 4;
    }
}
