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

        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas)), ReadOnly]
        public RenderTexture NumericalBrushAtlas { get; }

        [SerializeField]
        private List<SDFBrushNumerical> _numericalBrushes;

        [SerializeField]
        private List<SDFBrushAnalytical> _analyticalBrushes;

#if UNITY_EDITOR
        [Button]
        private void GatherBrushes()
        {
            _analyticalBrushes = new List<SDFBrushAnalytical>();
            var numericalBrushes = new List<SDFBrushNumericalWrapper>();
            foreach (var brush in SDFBrushHelper.EnabledSDFBrushes)
            {
                if (brush.GetBrush(out SDFBrushNumericalWrapper brushNumerical, out SDFBrushAnalytical brushAnalytical))
                    numericalBrushes.Add(brushNumerical);
                else
                    _analyticalBrushes.Add(brushAnalytical);
            }

            if (NumericalBrushAtlas)
            {
                string atlasPath = AssetDatabase.GetAssetPath(NumericalBrushAtlas);
                Debug.Assert(!string.IsNullOrEmpty(atlasPath), "Brush atlas not found");
                AssetDatabase.DeleteAsset(atlasPath);
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
