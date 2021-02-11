using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Antares.Utility;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

using FilePathAttribute = Sirenix.OdinInspector.FilePathAttribute;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject, ISerializationCallbackReceiver
    {
        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas))]
        public RenderTexture NumericalBrushAtlas { get; }

        [SerializeField, FilePath, Required]
        private string _serializationPath;

        private NativeArray<SDFBrush> _brushes;

        private NativeArray<float> _parameters;

        public void CopyBrushParameters(NativeArray<float> parameters) => _parameters.CopyTo(parameters);

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            using FileStream stream = new FileStream(_serializationPath, FileMode.OpenOrCreate);

            int parameterCount = _parameters.Length;
            SerializationUtility.SerializeValue(parameterCount, stream, DataFormat.Binary);

            for (int i = 0; i < _parameters.Length; i++)
                SerializationUtility.SerializeValue(_parameters[i], stream, DataFormat.Binary);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
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
            SDFBrushHelper[] brushHelpers = FindObjectsOfType<SDFBrushHelper>();
            _brushes = new NativeArray<SDFBrush>(brushHelpers.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var numericalBrushes = new List<Texture3D>();
            var numericalBrushIndices = new List<int>();
            SDFBrush brush = default;
            Texture3D numericalBrush = null;
            for (int i = 0; i < brushHelpers.Length; i++)
            {
                if (brushHelpers[i].GetBrush(ref brush, ref numericalBrush))
                {
                    numericalBrushes.Add(numericalBrush);
                    numericalBrushIndices.Add(i);
                }
                _brushes[i] = brush;
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
