// PriorityQueue.cs - Min-heap priority queue with proper naming conventions
using System;
using System.Collections.Generic;

namespace Azimuth.DES
{
    /// <summary>
    /// Thread-safe min-heap priority queue implementation.
    /// Items with lower comparable values have higher priority and are dequeued first.
    /// </summary>
    /// <typeparam name="T">Type that implements IComparable for priority comparison</typeparam>
    public class PriorityQueue<T> where T : IComparable<T>
    {
        private readonly List<T> heap;
        private readonly object lockObject = new object();

        public int Count 
        { 
            get 
            { 
                lock (lockObject) 
                { 
                    return heap.Count; 
                } 
            } 
        }

        public bool IsEmpty 
        { 
            get 
            { 
                lock (lockObject) 
                { 
                    return heap.Count == 0; 
                } 
            } 
        }

        public T First 
        { 
            get 
            { 
                lock (lockObject) 
                { 
                    if (heap.Count == 0)
                        throw new InvalidOperationException("Queue is empty");
                    return heap[0]; 
                } 
            } 
        }

        public PriorityQueue()
        {
            heap = new List<T>();
        }

        public PriorityQueue(int capacity)
        {
            heap = new List<T>(capacity);
        }

        public void Clear()
        {
            lock (lockObject)
            {
                heap.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (lockObject)
            {
                return heap.Contains(item);
            }
        }

        /// <summary>
        /// Remove a specific item from the queue and restore heap property
        /// </summary>
        public bool Remove(T item)
        {
            lock (lockObject)
            {
                int index = heap.IndexOf(item);
                if (index == -1) return false;

                // Move last item to removed position
                int lastIndex = heap.Count - 1;
                T lastItem = heap[lastIndex];
                heap.RemoveAt(lastIndex);

                // If we removed the last item, we're done
                if (index == lastIndex)
                    return true;

                // Replace removed item with last item
                heap[index] = lastItem;

                // Restore heap property by trying both directions
                // The item might need to go up or down depending on its value
                int parent = GetParentIndex(index);
                if (parent >= 0 && Compare(heap[index], heap[parent]) < 0)
                {
                    // Item is smaller than parent, bubble up
                    BubbleUp(index);
                }
                else
                {
                    // Item is larger than or equal to parent, bubble down
                    BubbleDown(index);
                }

                return true;
            }
        }

        /// <summary>
        /// Peek at the highest priority item without removing it
        /// </summary>
        public T Peek()
        {
            lock (lockObject)
            {
                if (heap.Count == 0)
                    throw new InvalidOperationException("Queue is empty");
                return heap[0];
            }
        }

        /// <summary>
        /// Add an item to the queue
        /// </summary>
        public void Push(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (lockObject)
            {
                // Add to end of heap
                heap.Add(item);
                
                // Restore heap property by bubbling up
                BubbleUp(heap.Count - 1);
            }
        }

        /// <summary>
        /// Remove and return the highest priority item
        /// </summary>
        public T Pop()
        {
            lock (lockObject)
            {
                if (heap.Count == 0)
                    throw new InvalidOperationException("Queue is empty");

                // Store the root (highest priority item)
                T result = heap[0];

                // Move last item to root
                int lastIndex = heap.Count - 1;
                heap[0] = heap[lastIndex];
                heap.RemoveAt(lastIndex);

                // Restore heap property by bubbling down from root
                if (heap.Count > 0)
                {
                    BubbleDown(0);
                }

                return result;
            }
        }

        /// <summary>
        /// Get all items as an array (for debugging/inspection)
        /// </summary>
        public T[] ToArray()
        {
            lock (lockObject)
            {
                return heap.ToArray();
            }
        }

        #region Heap Operations

        /// <summary>
        /// Compare two items (negative if a has higher priority than b)
        /// </summary>
        private int Compare(T a, T b)
        {
            return a.CompareTo(b);
        }

        /// <summary>
        /// Get the parent index of a node
        /// </summary>
        private int GetParentIndex(int index)
        {
            return (index - 1) / 2;
        }

        /// <summary>
        /// Get the left child index of a node
        /// </summary>
        private int GetLeftChildIndex(int index)
        {
            return 2 * index + 1;
        }

        /// <summary>
        /// Get the right child index of a node
        /// </summary>
        private int GetRightChildIndex(int index)
        {
            return 2 * index + 2;
        }

        /// <summary>
        /// Bubble up: Move an item up the heap until heap property is restored.
        /// Used when an item at the given index might be smaller than its parent.
        /// </summary>
        private void BubbleUp(int index)
        {
            if (index <= 0) return;

            T item = heap[index];
            int currentIndex = index;

            // Move up while the item is smaller than its parent
            while (currentIndex > 0)
            {
                int parentIndex = GetParentIndex(currentIndex);
                T parent = heap[parentIndex];

                // If item is not smaller than parent, heap property is satisfied
                if (Compare(item, parent) >= 0)
                    break;

                // Move parent down
                heap[currentIndex] = parent;
                currentIndex = parentIndex;
            }

            // Place item in its final position
            heap[currentIndex] = item;
        }

        /// <summary>
        /// Bubble down: Move an item down the heap until heap property is restored.
        /// Used when an item at the given index might be larger than its children.
        /// </summary>
        private void BubbleDown(int index)
        {
            int count = heap.Count;
            if (index >= count) return;

            T item = heap[index];
            int currentIndex = index;

            // Move down while we have children
            while (true)
            {
                int leftChildIndex = GetLeftChildIndex(currentIndex);
                
                // If no left child, we're at a leaf node
                if (leftChildIndex >= count)
                    break;

                int rightChildIndex = GetRightChildIndex(currentIndex);
                
                // Find the smaller child (higher priority)
                int smallerChildIndex = leftChildIndex;
                if (rightChildIndex < count && Compare(heap[rightChildIndex], heap[leftChildIndex]) < 0)
                {
                    smallerChildIndex = rightChildIndex;
                }

                T smallerChild = heap[smallerChildIndex];

                // If item is smaller than or equal to smallest child, heap property is satisfied
                if (Compare(item, smallerChild) <= 0)
                    break;

                // Move smaller child up
                heap[currentIndex] = smallerChild;
                currentIndex = smallerChildIndex;
            }

            // Place item in its final position
            heap[currentIndex] = item;
        }

        #endregion

        #region Debugging and Validation

        /// <summary>
        /// Validate that the heap property is maintained (for debugging)
        /// </summary>
        public bool ValidateHeap()
        {
            lock (lockObject)
            {
                for (int i = 0; i < heap.Count; i++)
                {
                    int leftChild = GetLeftChildIndex(i);
                    int rightChild = GetRightChildIndex(i);

                    if (leftChild < heap.Count && Compare(heap[i], heap[leftChild]) > 0)
                        return false;

                    if (rightChild < heap.Count && Compare(heap[i], heap[rightChild]) > 0)
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Get a string representation of the heap (for debugging)
        /// </summary>
        public override string ToString()
        {
            lock (lockObject)
            {
                if (heap.Count == 0)
                    return "PriorityQueue: Empty";

                return $"PriorityQueue: Count={heap.Count}, Top={heap[0]}";
            }
        }

        #endregion
    }
}
