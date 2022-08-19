using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    public class FluidEmitterComponent<T> : MonoBehaviour where T : IFluidEmitter, new()
    {
        private static LinkedList<T> _emitterInstances;

        private static List<T> _emitterInstancesList;

        public static List<T> GetEmitterInstances()
        {
            if (_emitterInstances == null)
                _emitterInstances = new LinkedList<T>();

            if (_emitterInstancesList == null)
                _emitterInstancesList = new List<T>();
            else
                _emitterInstancesList.Clear();

            LinkedListNode<T> node = _emitterInstances.First;
            while (node != null)
            {
                _emitterInstancesList.Add(node.Value);
                node = node.Next;
            }

            return _emitterInstancesList;
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
            _linkedListNode = _emitterInstances.AddLast(Emitter);
        }

        protected virtual void OnDisable()
        {
            _emitterInstances.Remove(_linkedListNode);
            _linkedListNode = null;
        }
    }
}
