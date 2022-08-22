using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    [ExecuteInEditMode]
    public class CubeFluidEmitterComponent : FluidEmitterComponent<FluidEmitter.Cube>
    {
        [field: SerializeField, LabelText(nameof(Flux)), Range(1, 1024)]
        public float Flux { get; set; }

        private float _fluxRemainder;

        protected override void OnEnable()
        {
            base.OnEnable();

            _fluxRemainder = 0f;
        }

        public override void Elapse(float deltaTime)
        {
            _fluxRemainder += Flux * deltaTime;

            int particleCount = Mathf.FloorToInt(_fluxRemainder);
            if (particleCount > 0)
            {
                Emitter.AddParticles(particleCount);
                _fluxRemainder -= particleCount;
            }

            Emitter.Offset = transform.position;
        }

#if UNITY_EDITOR

        private void Update()
        {
            Emitter.Offset = transform.position;
        }

#endif
    }
}
