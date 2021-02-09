using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Antares.Utility;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

using FilePathAttribute = Sirenix.OdinInspector.FilePathAttribute;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject, ISerializationCallbackReceiver
    {
        public IReadOnlyList<SDFBrushNumerical> NumericalBrushes => _numericalBrushes;

        public IReadOnlyList<SDFBrushAnalytical> AnalyticalBrushes => _analyticalBrushes;

        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas))]
        public RenderTexture NumericalBrushAtlas { get; }

        [SerializeField]
        private List<SDFBrushNumerical> _numericalBrushes;

        [SerializeField]
        private List<SDFBrushAnalytical> _analyticalBrushes;

        [SerializeField, FilePath]
        private string _serializationPath;

        private NativeArray<float> _parameters;

        public void OnBeforeSerialize()
        {
            using FileStream stream = new FileStream(_serializationPath, FileMode.OpenOrCreate);

            int parameterCount = _parameters.Length;
            SerializationUtility.SerializeValue(parameterCount, stream, DataFormat.Binary);

            for (int i = 0; i < _parameters.Length; i++)
                SerializationUtility.SerializeValue(_parameters[i], stream, DataFormat.Binary);
        }

        public void OnAfterDeserialize()
        {
            using FileStream stream = new FileStream(_serializationPath, FileMode.Open);

            int parameterCount = SerializationUtility.DeserializeValue<int>(stream, DataFormat.Binary);
            _parameters = new NativeArray<float>(parameterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < parameterCount; i++)
                _parameters[i] = SerializationUtility.DeserializeValue<float>(stream, DataFormat.Binary);
        }

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
