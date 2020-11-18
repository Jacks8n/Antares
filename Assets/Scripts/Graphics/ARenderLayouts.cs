using UnityEngine;

namespace Antares.Graphics
{
    class ARenderLayouts
    {
        private ARenderLayouts() { }

        public enum Attachments { Depth, GBuffer0, Shading, Max };

        public const int AttachmentCount = (int)Attachments.Max;
        public const int AttachmentIndex_Depth = (int)Attachments.Depth;
        public const int AttachmentIndex_GBuffer0 = (int)Attachments.GBuffer0;
        public const int AttachmentIndex_Shading = (int)Attachments.Shading;

        public const string Binding_SceneSDF = "SceneVolume";
        public const string Binding_Depth = "Depth";
        public const string Binding_GBuffer0 = "GBuffer0";
        public const string Binding_Shading = "Shading";
        public const string Binding_SceneRM0 = "SceneRM0";
        public const string Binding_RMParams = "RMParams";

        public static readonly int ID_SceneSDF = Shader.PropertyToID(Binding_SceneSDF);
        public static readonly int ID_Depth = Shader.PropertyToID(Binding_Depth);
        public static readonly int ID_GBuffer0 = Shader.PropertyToID(Binding_GBuffer0);
        public static readonly int ID_Shading = Shader.PropertyToID(Binding_Shading);
        public static readonly int ID_SceneRM0 = Shader.PropertyToID(Binding_SceneRM0);
        public static readonly int ID_RMParams = Shader.PropertyToID(Binding_RMParams);

        private const uint RayMarchingGroupX = 4;
        private const uint RayMarchingGroupY = 4;
        private const uint RayMarchingGroupZ = 1;

        public static readonly Mesh FullScreenMesh;

        static ARenderLayouts()
        {
            FullScreenMesh = new Mesh() {
                vertices = new Vector3[] { Vector3.zero, new Vector3(2f, 0f), new Vector3(0f, 2f) },
                uv = new Vector2[] { Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, 2f) },
                triangles = new int[] { 0, 2, 1 },
            };
        }

        public static Vector3Int GetRMDispatchGroups(int width, int height, uint kernelX, uint kernelY, uint kernelZ)
        {
            Debug.Assert(kernelZ == 1);

            uint threadX = kernelX * RayMarchingGroupX;
            uint threadY = kernelY * RayMarchingGroupY;

            if (width % threadX != 0 || height % threadY != 0)
                Debug.LogWarning("dispath won't cover full screen");

            return new Vector3Int(
                (int)(width / threadX),
                (int)(height / threadY),
                (int)RayMarchingGroupZ);
        }
    }
}
