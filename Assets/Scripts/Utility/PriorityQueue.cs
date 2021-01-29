using System;
using System.Collections.Generic;

namespace Antares.Utility
{
    public class PriorityQueue<T> where T : IComparable<T>
    {
        private readonly List<T> _elements;

        public PriorityQueue() : this(new List<T>()) { }

        public PriorityQueue(List<T> underlyingList) => _elements = underlyingList;

        public T this[int index] => _elements[index];

        public List<T> UnderlyingList => _elements;

        public int Count => _elements.Count;

        public bool Empty => Count == 0;

        public void Enqueue(T item)
        {
            int child = Count;
            int parent = child / 2;

            _elements.Add(item);
            while (IsSmaller(parent, child))
            {
                Swap(child, parent);

                if (parent == 0)
                    break;

                child = parent;
                parent /= 2;
            }
        }

        public T Peek() => _elements[0];

        public T Dequeue()
        {
            T item = Peek();

            int parent = 0;
            int child = 1;

            while (child + 1 < Count)
            {
                if (IsSmaller(child, child + 1))
                    ++child;
                _elements[parent] = _elements[child];
                parent = child;
                child *= 2;
            }
            if (child < Count)
                _elements[parent] = _elements[child];

            _elements.RemoveAt(Count - 1);
            return item;
        }

        private void Swap(int left, int right)
        {
            T temp;
            temp = _elements[left];
            _elements[left] = _elements[right];
            _elements[right] = temp;
        }

        private bool IsSmaller(int left, int right) => _elements[left].CompareTo(_elements[right]) < 0;
    }

    public static class PriorityQueueExtension
    {
        public static PriorityQueue<T> ToPriorityQueue<T>(this List<T> list) where T : IComparable<T> => new PriorityQueue<T>(list);
    }
}
