using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    public class FluidEmitterComponent<T> : MonoBehaviour where T : IFluidEmitter, new()
    {
        public static LinkedList<T> EmitterInstances
        {
            get
            {
                if (_emitterInstances == null)
                    _emitterInstances = new LinkedList<T>();

                return _emitterInstances;
            }
        }

        private static LinkedList<T> _emitterInstances;

        private static List<T> _emitterInstanceList;

        public static List<T> GetEmitterInstanceList()
        {
            if (_emitterInstanceList == null)
                _emitterInstanceList = new List<T>();
            else
                _emitterInstanceList.Clear();

            LinkedListNode<T> node = EmitterInstances.First;
            while (node != null)
            {
                _emitterInstanceList.Add(node.Value);
                node = node.Next;
            }

            return _emitterInstanceList;
        }

        [field: SerializeField, LabelText(nameof(Emitter))]
        public T Emitter { get; private set; }

        private LinkedListNode<T> _linkedListNode;

        protected virtual void Awake()
        {
            Emitter = new T();
        }

        protected virtual void OnEnable()
        {
            Emitter.ClearParticles();

            _linkedListNode = EmitterInstances.AddLast(Emitter);
        }

        protected virtual void OnDisable()
        {
            EmitterInstances.Remove(_linkedListNode);
            _linkedListNode = null;
        }
    }
}
