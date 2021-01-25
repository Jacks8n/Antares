using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject
    {
        public IReadOnlyList<SDFBrushNumerical> NumericalBrushes => _numericalBrushes;

        public IReadOnlyList<SDFBrushAnalytical> AnalyticalBrushes => _analyticalBrushes;

        [SerializeField]
        private List<SDFBrushNumerical> _numericalBrushes;

        [SerializeField]
        private List<SDFBrushAnalytical> _analyticalBrushes;

#if UNITY_EDITOR
        [Button]
        private void GatherBrushes()
        {
            _numericalBrushes = new List<SDFBrushNumerical>();
            _analyticalBrushes = new List<SDFBrushAnalytical>();
            foreach (var brush in SDFBrushHelper.EnabledSDFBrushes)
            {
                if (brush.GetBrush(out SDFBrushNumerical brushNumerical, out SDFBrushAnalytical brushAnalytical))
                    _numericalBrushes.Add(brushNumerical);
                else
                    _analyticalBrushes.Add(brushAnalytical);
            }
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
