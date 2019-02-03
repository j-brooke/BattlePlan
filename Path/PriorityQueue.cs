using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BattlePlan.Path
{
    /// <summary>
    /// Collection where each items are enqueued with specific priority values, and they
    /// are dequeued in the order of the lowest prioity value first.
    /// </summary>
    /// <remarks>
    /// This class is not up to .NET standards for general purpose collections.  It should implement
    /// ICollection for one.  It should allow the caller to give a comparison function object, as well.
    /// </remarks>
    public class PriorityQueue<PrType,ItemType>
        where PrType : IComparable<PrType>
    {
        public int Count => _count;

        public PriorityQueue()
            : this(_defaultInitialCapacity)
        {
        }

        public PriorityQueue(int initialCapacity)
        {
            _heap = new PriorityItem[initialCapacity];
            _count = 0;
        }

        public void Clear()
        {
            Array.Clear(_heap, 0, _heap.Length);
            _count = 0;
        }

        public void Enqueue(PrType priority, ItemType item)
        {
            if (_count==_heap.Length)
                Array.Resize(ref _heap, _heap.Length*2);

            var newIdx = _count;
            _heap[newIdx] = new PriorityItem(priority, item);
            _count += 1;

            ShiftUp(newIdx);
        }

        public ItemType Peek()
        {
            if (_count==0)
                throw new InvalidOperationException();
            return _heap[0].item;
        }

        public ItemType Dequeue()
        {
            if (_count==0)
                throw new InvalidOperationException();

            var itemToReturn = _heap[0].item;

            _heap[0] = _heap[_count-1];
            _heap[_count-1] = default(PriorityItem);
            _count -= 1;

            ShiftDown(0);

            return itemToReturn;
        }

        public bool Contains(ItemType testItem)
        {
            // Loop through all entries.  This is O(n) of course.  We could make this more efficient
            // by keeping a Dictionary<ItemType,int> that tracks a count of each value.  That would add
            // some overhead to Enqueue and Dequeue but wouldn't hurt their algorithmic efficiency.
            foreach (var prEntry in _heap)
            {
                if (prEntry.item.Equals(testItem))
                    return true;
            }
            return false;
        }

        public void Remove(ItemType item)
        {
            for (int i=_count-1; i>=0; --i)
            {
                if (_heap[i].item.Equals(item))
                {
                    _heap[i] = _heap[_count-1];
                    _heap[_count-1] = default(PriorityItem);
                    _count -= 1;

                    ShiftDown(i);
                    return;
                }
            }
        }

        private const int _defaultInitialCapacity = 512;
        private PriorityItem[] _heap;
        private int _count;


        private void SwapAt(int indexA, int indexB)
        {
            var temp = _heap[indexA];
            _heap[indexA] = _heap[indexB];
            _heap[indexB] = temp;
        }

        private void ShiftUp(int startIndex)
        {
            var idx = startIndex;
            while (idx > 0)
            {
                var parIdx = IndexOfParent(idx);
                if (IsHeapier(idx, parIdx))
                {
                    SwapAt(idx, parIdx);
                    idx = parIdx;
                }
                else
                {
                    return;
                }
            }
        }

        private void ShiftDown(int startIndex)
        {
            var idx = startIndex;
            while (true)
            {
                var leftIdx = IndexOfLeftChild(idx);
                var rightIdx = leftIdx+1;
                bool leftIsHeapier = leftIdx<_count && IsHeapier(leftIdx, idx);
                bool rightIsHeapier = rightIdx<_count && IsHeapier(rightIdx, idx);
                bool swapRight = rightIsHeapier && IsHeapier(rightIdx, leftIdx);
                bool swapLeft = leftIsHeapier;
                if (swapRight)
                {
                    SwapAt(rightIdx, idx);
                    idx = rightIdx;
                }
                else if (swapLeft)
                {
                    SwapAt(leftIdx, idx);
                    idx = leftIdx;
                }
                else
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Abstraction of the comparison.  Right now we're locked in as a min-heap, so
        /// IsHeapier means less-than, but if we want to make this structure more general
        /// we'd want to allow a choice between min or max, at the very least.  Better would
        /// be to allow the caller to give their own comparison function.
        /// </summary>
        private bool IsHeapier(int indexA, int indexB)
        {
            return _heap[indexA].priority.CompareTo(_heap[indexB].priority) < 0;
        }

        private static int IndexOfParent(int currentIndex)
        {
            return (currentIndex-1) / 2;
        }

        private static int IndexOfLeftChild(int currentIndex)
        {
            return currentIndex*2 + 1;
        }

        private static int IndexOfRightChild(int currentIndex)
        {
            return currentIndex*2 + 2;
        }

        private struct PriorityItem
        {
            public PrType priority;
            public ItemType item;

            public PriorityItem(PrType newPriority, ItemType newItem)
            {
                this.priority = newPriority;
                this.item = newItem;
            }
        }
    }
}