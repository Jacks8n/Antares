using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class DeferredGraphics : IShaderSpec
        {
            [field: SerializeField, LabelText(nameof(Material))]
            public Material Material { get; private set; }

            void IShaderSpec.Initialize()
            {
            }
        }
    }
}
