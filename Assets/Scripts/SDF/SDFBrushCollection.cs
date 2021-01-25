using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject
    {
        public IReadOnlyList<SDFBrush> Brushes => _brushes;

        [SerializeField]
        private List<SDFBrush> _brushes;

#if UNITY_EDITOR
        [Button]
        private void GatherBrushes()
        {
            _brushes = new List<SDFBrush>();
            foreach (var brush in SDFBrushHelper.EnabledSDFBrushes)
                _brushes.Add(brush.Brush);
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
