using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public class DeferredGraphics : IShaderSpec
        {
            [field: SerializeField, LabelText(nameof(Material))]
            public Material Material { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
            }
        }
    }
}
