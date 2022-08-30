using Antares.Graphics;
using Antares.Physics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.SDF
{
    [ExecuteAlways]
    public class SDFScene : MonoBehaviour
    {
        public static SDFScene Instance { get; private set; }

        [field: SerializeField, LabelText("Scene Resolution")]
        [field: InfoBox("Specifications are applied while the scene is being loaded. "
            + "Reloading is required if any of them is modified.")]
        [field: VerticalGroup("Specification")]
        public Vector3Int Size { get; private set; }

        [field: SerializeField, Required, LabelText(nameof(BrushCollection))]
        [field: VerticalGroup("Specification")]
        public SDFBrushCollection BrushCollection { get; private set; }

        [field: SerializeField, Required, LabelText(nameof(PhysicsScene))]
        [field: VerticalGroup("Specification")]
        public APhysicsScene PhysicsScene { get; private set; }

        public Vector3 SizeInFloat => Size;

        public Vector3 SizeInv => new Vector3(1f / Size.x, 1f / Size.y, 1f / Size.z);

        public bool IsEmpty => BrushCollection.Brushes.Length == 0;

        public Matrix4x4 SceneToWorld => transform.localToWorldMatrix;

        public Matrix4x4 WorldToScene => transform.worldToLocalMatrix;

        public float GridSizeWorld => transform.localScale.x;

        public float WorldSpaceSupremum => GridSizeWorld * AShaderSpecifications.SDFSupremum;

        public Vector3 WorldSpaceBoundMin => transform.position;

        public Vector3 WorldSpaceBoundMax => transform.position + SizeInFloat;

        public Vector3 WorldToSceneVector(Vector3 vec) => transform.InverseTransformVector(vec);

        public Vector3 WorldToScenePoint(Vector3 pos) => transform.InverseTransformPoint(pos);

        private void OnEnable()
        {
            if (Instance)
                Instance.enabled = false;
            Instance = this;

            if (ARenderPipeline.Instance != null)
                ARenderPipeline.Instance.LoadScene(this);
        }

        private void OnDisable()
        {
            Instance = null;

            if (ARenderPipeline.Instance != null)
                ARenderPipeline.Instance.UnloadScene();
        }

        private void Update()
        {
#if UNITY_EDITOR
            Vector3 scale = transform.localScale;
            if (scale.x != scale.y || scale.y != scale.z || scale.z != scale.x)
            {
                Debug.LogWarning("Only the affine transfrom is permitted");
                transform.localScale = new Vector3(scale.x, scale.x, scale.x);
            }
#endif
        }

#if UNITY_EDITOR

        [Button]
        private void CaptureSceneConstruction()
        {
            ARenderPipeline.AddCaptureEvent(() =>
            {
                if (enabled)
                    enabled = false;

                enabled = true;
            });
        }

#endif
    }
}
