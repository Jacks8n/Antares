using UnityEngine;

namespace Antares.Graphics
{
    public class AQualitySettings
    {
        public static int DirectionalCascadeCount => QualitySettings.shadowCascades;

        public static int DirectionalShadowResolution { get => 2048; }
    }
}
