using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    public class FluidEmitterComponent<T> : MonoBehaviour where T : IFluidEmitter, new()
    {
        public static LinkedList<FluidEmitterComponent<T>> EmitterComponentInstances
        {
            get
            {
                if (_emitterComponentInstances == null)
                    _emitterComponentInstances = new LinkedList<FluidEmitterComponent<T>>();

                return _emitterComponentInstances;
            }
        }

        private static LinkedList<FluidEmitterComponent<T>> _emitterComponentInstances;

        private static List<T> _emitterInstanceList;

        public static List<T> GetEmitterInstanceList(float deltaTime = 0f)
        {
            if (_emitterInstanceList == null)
                _emitterInstanceList = new List<T>();
            else
                _emitterInstanceList.Clear();

            LinkedListNode<FluidEmitterComponent<T>> node = EmitterComponentInstances.First;
            while (node != null)
            {
                FluidEmitterComponent<T> component = node.Value;

                if (deltaTime > 0f)
                    component.Elapse(deltaTime);

                _emitterInstanceList.Add(component.Emitter);
                node = node.Next;
            }

            return _emitterInstanceList;
        }

        [field: SerializeField, LabelText(nameof(Emitter))]
        public T Emitter { get; private set; }

        private LinkedListNode<FluidEmitterComponent<T>> _linkedListNode;

        protected virtual void OnEnable()
        {
            Emitter.ClearParticles();

            _linkedListNode = EmitterComponentInstances.AddLast(this);
        }

        public virtual void Elapse(float deltaTime)
        {
        }

        protected virtual void OnDisable()
        {
            EmitterComponentInstances.Remove(_linkedListNode);
            _linkedListNode = null;
        }
    }
}
