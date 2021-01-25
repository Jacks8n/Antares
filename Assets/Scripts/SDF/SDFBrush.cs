using System;
using UnityEngine;

namespace Antares.SDF
{
    [Serializable]
    public struct SDFBrush
    {
        [field: SerializeField]
        public Vector3 Position { get; }

        [field: SerializeField]
        public Quaternion Rotation { get; }

        [field: SerializeField]
        public Vector3 Scale { get; }

        [field: SerializeField]
        public SDFPresentation SDF { get; }

        public SDFBrush(Transform transform, SDFPresentation sdf)
        {
            Position = transform.position;
            Rotation = transform.rotation;
            Scale = transform.localScale;
            SDF = sdf;
        }
    }
}
