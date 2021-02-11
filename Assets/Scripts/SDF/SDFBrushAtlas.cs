using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushAtlas")]
    public class SDFBrushAtlas : ScriptableObject
    {
#if UNITY_EDITOR
        [SerializeField]
        private List<Texture3D> _bakedBrushes;

        [Button]
        private void Batch()
        {

        }
#endif
    }
}
