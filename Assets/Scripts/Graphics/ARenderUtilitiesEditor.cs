using UnityEngine;
using UnityEditorInternal;

namespace Antares.Graphics
{
    public static partial class ARenderUtilities
    {
#if UNITY_EDITOR

        public static Mesh FullScreenSceneViewMesh
        {
            get
            {
                if (!_fullScreenSceneViewMesh)
                    _fullScreenSceneViewMesh = new Mesh()
                    {
                        vertices = new Vector3[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                        triangles = new int[] { 0, 1, 2 },
                        uv = new Vector2[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(2f, 1f) }
                    };
                return _fullScreenSceneViewMesh;
            }
        }

        private static Mesh _fullScreenSceneViewMesh = null;

        public static void BeginCaptureSceneView()
        {
            if (!RenderDoc.IsLoaded())
            {
                Debug.LogWarning("enable render doc manually first");
                return;
            }

            var window = UnityEditor.EditorWindow.GetWindow<UnityEditor.SceneView>();
            RenderDoc.BeginCaptureRenderDoc(window);
        }

        public static void EndCaptureSceneView()
        {
            var window = UnityEditor.EditorWindow.GetWindow<UnityEditor.SceneView>();
            RenderDoc.EndCaptureRenderDoc(window);
        }

#endif
    }
}
