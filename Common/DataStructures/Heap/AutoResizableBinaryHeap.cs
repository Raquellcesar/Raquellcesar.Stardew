// -----------------------------------------------------------------------
// <copyright file="AutoResizableBinaryHeap.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    using Raquellcesar.Stardew.Common.Utilities;

    /// <summary>
    ///     Implements a binary heap, a heap data structure that takes the form of a binary tree,
    ///     that auto-resizes.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of the values in the heap. It must be a reference type.
    /// </typeparam>
    public abstract class AutoResizableBinaryHeap<T> : IHeap<T>
        where T : class
    {
        /// <summary>
        ///     The default initial capacity for the heap.
        /// </summary>
        protected const int InitialHeapSize = 10;

        /// <summary>
        ///     The comparer used for comparisons between objects.
        /// </summary>
        protected readonly IComparer<T> Comparer;

        /// <summary>
        ///     Backing field for the <see cref="Count"/> property.
        /// </summary>
        private int count;

        /// <summary>
        ///     The internal array used to store the heap elements.
        /// </summary>
        private T[] heapArray;

        /// <summary>
        ///     An internal dictionary that maps elements to their position in the heap, allowing
        ///     the search for one of its elements to be performed in O(1) time.
        /// </summary>
        private readonly Dictionary<T, int> indexes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoResizableBinaryHeap{T}"/> class. It
        ///     will be a min-heap that uses <see cref="Comparer{T}.Default"/> to compare items.
        /// </summary>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="InitialHeapSize"/>.</param>
        protected AutoResizableBinaryHeap(int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(HeapType.Min, null, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoResizableBinaryHeap{T}"/> class. It
        ///     will use <see cref="Comparer{T}.Default"/> to compare items.
        /// </summary>
        /// <param name="heapType">Specifies whether this will be a min or max heap.</param>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="InitialHeapSize"/>.</param>
        protected AutoResizableBinaryHeap(HeapType heapType, int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(heapType, null, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoResizableBinaryHeap{T}"/> class. It
        ///     will be a min-heap.
        /// </summary>
        /// <param name="comparer">The comparer used to compare items.</param>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="InitialHeapSize"/>.</param>
        protected AutoResizableBinaryHeap(
            IComparer<T> comparer,
            int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(HeapType.Min, comparer, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoResizableBinaryHeap{T}"/> class.
        /// </summary>
        /// <param name="heapType">Specifies whether this will be a min or max heap.</param>
        /// <param name="comparer">The comparer used to compare items.</param>
        /// <param name="capacity">
        ///     The initial capacity of the heap. Must be greater than 0, defaults to <see cref="InitialHeapSize"/>.
        /// </param>
        protected AutoResizableBinaryHeap(
            HeapType heapType,
            IComparer<T> comparer,
            int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
        {
            if (capacity <= 0)
            {
                throw new InvalidOperationException("The heap capacity cannot be smaller than 1.");
            }

            this.Comparer = comparer is not null ? new HeapNodeComparer<T>(comparer, heapType) : new HeapNodeComparer<T>(Comparer<T>.Default, heapType);

            this.heapArray = new T[capacity + 1];
            this.indexes = new Dictionary<T, int>(new ObjectReferenceComparer<T>());
            this.count = 0;
        }

        /// <inheritdoc/>
        public int Count => this.count;

        /// <inheritdoc/>
        /// <remarks>Runs in O(n) time.</remarks>
        public void Clear()
        {
            Array.Clear(this.heapArray, 1, this.count);
            this.indexes.Clear();
            this.count = 0;
            this.ClearInternal();
        }

        /// <inheritdoc/>
        /// <remarks>Runs in O(1) time.</remarks>
        /// <exception cref="ArgumentNullException">The argument is null.</exception>
        public bool Contains(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return this.indexes.ContainsKey(item);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            IEnumerable<T> enumerable = new ArraySegment<T>(this.heapArray, 1, this.count);
            return enumerable.GetEnumerator();
        }

        /// <summary>
        ///     Call this method after an item in the heap changes to restore heap condition.
        /// </summary>
        /// <param name="item">The item that needs to be moved up or down the heap.</param>
        /// <exception cref="ArgumentNullException">The argument is null.</exception>
        /// <exception cref="InvalidOperationException">The item is not in the heap.</exception>
        public void Heapify(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!this.Contains(item))
            {
                throw new InvalidOperationException("Cannot call Update() on an item that is not in the heap: " + item);
            }

            this.HeapifyInternal(item);
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return this.count == 0;
        }

        /// <summary>
        ///     Checks to make sure the heap is in a valid state, i.e. it satisfies the heap
        ///     property. Used for testing/debugging the heap.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the heap satisfies the heap property, <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public bool IsValid()
        {
            for (int i = 1; i <= this.count; i++)
            {
                int childLeftIndex = 2 * i;
                if (childLeftIndex <= this.count && this.heapArray[childLeftIndex] is not null && this.IsLessThan(
                        this.heapArray[childLeftIndex],
                        this.heapArray[i]))
                {
                    return false;
                }

                int childRightIndex = childLeftIndex + 1;
                if (childRightIndex <= this.count && this.heapArray[childRightIndex] is not null && this.IsLessThan(
                        this.heapArray[childRightIndex],
                        this.heapArray[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        /// <remarks>Runs in O(1) time.</remarks>
        /// <exception cref="InvalidOperationException">The heap is empty.</exception>
        public T Peek()
        {
            if (this.count == 0)
            {
                throw new InvalidOperationException("Cannot call .Peek() on an empty heap.");
            }

            return this.heapArray[1];
        }

        /// <inheritdoc/>
        /// <remarks>Runs in O(log n) time.</remarks>
        /// <exception cref="InvalidOperationException">The heap is empty.</exception>
        public T Pop()
        {
            if (this.count == 0)
            {
                throw new InvalidOperationException("Cannot call .Pop() on an empty heap.");
            }

#if DEBUG
            if (!this.IsValid())
            {
                throw new InvalidOperationException(
                    "The heap has been corrupted (Did you update a node value manually without calling Heapify()?)");
            }

#endif

            // Get the root node.
            T minMax = this.heapArray[1];
            this.indexes.Remove(minMax);
            this.RemoveInternal(minMax);

            // If the node is the last node, we can remove it immediately.
            if (this.count == 1)
            {
                this.heapArray[1] = null;
                this.count = 0;
                return minMax;
            }

            // Move the last item to the root node.
            T formerLastNode = this.heapArray[this.count];
            this.indexes[formerLastNode] = 1;
            this.heapArray[1] = formerLastNode;
            this.heapArray[this.count] = null;
            this.count--;

            this.HeapifyDown(formerLastNode);

            return minMax;
        }

        /// <inheritdoc/>
        /// <remarks>Runs in O(log n) time.</remarks>
        /// <exception cref="ArgumentNullException">The argument is null.</exception>
        /// <exception cref="InvalidOperationException">The item is already in the heap.</exception>
        public void Push(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (this.Contains(item))
            {
                throw new InvalidOperationException("The item is already in the heap: " + item);
            }

            if (this.count == this.heapArray.Length - 1)
            {
                this.DoubleArray();
            }

            // Put the item at the end of the heap.
            this.count++;
            this.indexes[item] = this.count;
            this.heapArray[this.count] = item;
            this.AddInternal(item);

            this.HeapifyUp(item);
        }

        /// <summary>
        ///     Removes an item from the heap. The item does not need to be the root of the heap.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <exception cref="ArgumentNullException">The argument is null.</exception>
        /// <exception cref="InvalidOperationException">The item is not in the heap.</exception>
        public void Remove(T item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!this.Contains(item))
            {
                throw new InvalidOperationException("Cannot call Remove() on an item that is not in the heap: " + item);
            }

            // If the node is the last node, we can remove it immediately.
            if (this.indexes[item] == this.count)
            {
                this.indexes.Remove(item);
                this.heapArray[this.count] = null;
                this.RemoveInternal(item);
                this.count--;
                return;
            }

            // Move the last node to the item's position.
            T formerLastNode = this.heapArray[this.count];
            this.indexes[formerLastNode] = this.indexes[item];
            this.heapArray[this.indexes[item]] = formerLastNode;
            this.indexes.Remove(item);
            this.RemoveInternal(item);
            this.heapArray[this.count] = null;
            this.count--;

            // Now bubble formerLastNode (which is no longer the last node) up or down as appropriate.
            this.HeapifyInternal(formerLastNode);
        }

        /// <summary>
        ///     Removes an item from the heap and pushes a new item. More efficient than <see
        ///     cref="Remove(T)"/> followed by <see cref="Push(T)"/> (or vice versa), since we only
        ///     need to balance once, not twice.
        /// </summary>
        /// <param name="item">The item to remove from the heap.</param>
        /// <param name="newItem">The item to add to the heap.</param>
        /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
        /// <exception cref="InvalidOperationException">
        ///     The item to be removed is not in the heap or the item to be added is already in the heap.
        /// </exception>
        public void Replace(T item, T newItem)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (newItem is null)
            {
                throw new ArgumentNullException(nameof(newItem));
            }

            if (!this.Contains(item))
            {
                throw new InvalidOperationException(
                    "Cannot call Replace() to replace an item that is not in the heap: " + item);
            }

            if (this.Contains(newItem))
            {
                throw new InvalidOperationException(
                    "Cannot call Replace() to replace with an an item that is already in the heap: " + newItem);
            }

            // Replace the item.
            this.indexes[newItem] = this.indexes[item];
            this.heapArray[this.indexes[item]] = newItem;
            this.AddInternal(newItem);
            this.indexes.Remove(item);
            this.RemoveInternal(item);

            // Now bubble the new item up or down as appropriate.
            this.HeapifyInternal(newItem);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        ///     A function for subclasses to implement additional operations on item removal.
        /// </summary>
        /// <param name="item">The item removed from the heap.</param>
        protected abstract void AddInternal(T item);

        /// <summary>
        ///     A function for subclasses to implement additional operations when the heap is cleared.
        /// </summary>
        protected abstract void ClearInternal();

        /// <summary>
        ///     Checks if one item is less than other.
        /// </summary>
        /// <remarks>
        ///     Note that calling IsLessThan(item, item) (i.e. both arguments the same node) will
        ///     return false.
        /// </remarks>
        /// <param name="item1">The first item to compare.</param>
        /// <param name="item2">The second item to compare.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the first item is less than the second item.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        protected virtual bool IsLessThan(T item1, T item2)
        {
            return this.Comparer.Compare(item1, item2) < 0;
        }

        /// <summary>
        ///     A function for subclasses to implement additional operations on item addition.
        /// </summary>
        /// <param name="item">The item added to the heap.</param>
        protected abstract void RemoveInternal(T item);

        /// <summary>
        ///     Doubles the size of the internal array.
        /// </summary>
        private void DoubleArray()
        {
            T[] biggerArray = new T[(this.count * 2) + 1];
            Array.Copy(this.heapArray, biggerArray, this.count + 1);
            this.heapArray = biggerArray;
        }

        /// <summary>
        ///     Move a node down in the tree, as long as needed, in order to restore heap condition.
        /// </summary>
        /// <param name="current">The item to move down.</param>
        private void HeapifyDown(T current)
        {
            int currentIndex = this.indexes[current];
            int childLeftIndex = currentIndex << 1;

            // If the item is in the leaf node, we're done.
            if (childLeftIndex > this.count)
            {
                return;
            }

            // Check if the left child is lower than the current node.
            int childRightIndex = childLeftIndex + 1;
            T childLeft = this.heapArray[childLeftIndex];
            if (this.IsLessThan(childLeft, current))
            {
                // Check if there is a right child. If not, swap and finish.
                if (childRightIndex > this.count)
                {
                    this.indexes[current] = childLeftIndex;
                    this.indexes[childLeft] = currentIndex;
                    this.heapArray[currentIndex] = childLeft;
                    this.heapArray[childLeftIndex] = current;
                    return;
                }

                // Check if the left child is lower than the right child.
                T childRight = this.heapArray[childRightIndex];
                if (this.IsLessThan(childLeft, childRight))
                {
                    // Left is lower, move it up and continue.
                    this.indexes[childLeft] = currentIndex;
                    this.heapArray[currentIndex] = childLeft;
                    currentIndex = childLeftIndex;
                }
                else
                {
                    // Right is even lower, move it up and continue.
                    this.indexes[childRight] = currentIndex;
                    this.heapArray[currentIndex] = childRight;
                    currentIndex = childRightIndex;
                }
            }
            else if (childRightIndex > this.count)
            {
                // Not swapping with left child and right child doesn't exist, we're done.
                return;
            }
            else
            {
                // Check if the right child is lower than the current node.
                T childRight = this.heapArray[childRightIndex];
                if (this.IsLessThan(childRight, current))
                {
                    this.indexes[childRight] = currentIndex;
                    this.heapArray[currentIndex] = childRight;
                    currentIndex = childRightIndex;
                }
                else
                {
                    // Neither child is lower than current, so we're done.
                    return;
                }
            }

            // Continue moving down the tree.
            while (true)
            {
                childLeftIndex = currentIndex << 1;

                // If the node at current index is a leaf node, we're done.
                if (childLeftIndex > this.count)
                {
                    this.indexes[current] = currentIndex;
                    this.heapArray[currentIndex] = current;
                    break;
                }

                // Check if the left child is lower than the current node.
                childRightIndex = childLeftIndex + 1;
                childLeft = this.heapArray[childLeftIndex];
                if (this.IsLessThan(childLeft, current))
                {
                    // Check if there is a right child. If not, swap and finish.
                    if (childRightIndex > this.count)
                    {
                        this.indexes[current] = childLeftIndex;
                        this.indexes[childLeft] = currentIndex;
                        this.heapArray[currentIndex] = childLeft;
                        this.heapArray[childLeftIndex] = current;
                        break;
                    }

                    // Check if the left child is lower than the right child.
                    T childRight = this.heapArray[childRightIndex];
                    if (this.IsLessThan(childLeft, childRight))
                    {
                        // Left is lower, move it up and continue.
                        this.indexes[childLeft] = currentIndex;
                        this.heapArray[currentIndex] = childLeft;
                        currentIndex = childLeftIndex;
                    }
                    else
                    {
                        // Right is even lower, move it up and continue.
                        this.indexes[childRight] = currentIndex;
                        this.heapArray[currentIndex] = childRight;
                        currentIndex = childRightIndex;
                    }
                }
                else if (childRightIndex > this.count)
                {
                    // Not swapping with left child and right child doesn't exist, we're done.
                    this.indexes[current] = currentIndex;
                    this.heapArray[currentIndex] = current;
                    break;
                }
                else
                {
                    // Check if the right child is lower than the current node.
                    T childRight = this.heapArray[childRightIndex];
                    if (this.IsLessThan(childRight, current))
                    {
                        this.indexes[childRight] = currentIndex;
                        this.heapArray[currentIndex] = childRight;
                        currentIndex = childRightIndex;
                    }
                    else
                    {
                        // Neither child is lower than current, so finish and stop.
                        this.indexes[current] = currentIndex;
                        this.heapArray[currentIndex] = current;
                        break;
                    }
                }
            }
        }

        /// <summary>
        ///     Heapify up or down an out of order item.
        /// </summary>
        /// <param name="item">The item to move in the heap.</param>
        private void HeapifyInternal(T item)
        {
            // Bubble the updated node up or down as appropriate.
            int parentIndex = this.indexes[item] >> 1;

            if (parentIndex > 0 && this.IsLessThan(item, this.heapArray[parentIndex]))
            {
                this.HeapifyUp(item);
            }
            else
            {
                // Note that HeapifyDown will be called if parentNode == node (that is, node is the root)
                this.HeapifyDown(item);
            }
        }

        /// <summary>
        ///     Move a node up in the tree, as long as needed, in order to restore heap condition.
        /// </summary>
        /// <param name="current">The item to move up.</param>
        private void HeapifyUp(T current)
        {
            int currentIndex = this.indexes[current];

            // We're at the root.
            if (currentIndex == 1)
            {
                return;
            }

            // Compare the current item to its parent.
            int parentIndex = currentIndex >> 1;
            T parent = this.heapArray[parentIndex];

            // The current item is greater than or equal to its parent, we're done.
            if (!this.IsLessThan(current, parent))
            {
                return;
            }

            // The current item has a value less than or equal to the parent, so move the parent
            // down to make room
            this.indexes[parent] = currentIndex;
            this.heapArray[currentIndex] = parent;

            currentIndex = parentIndex;

            // Move up the tree until we find a parent with value less than or equal to the current item.
            while (parentIndex > 1)
            {
                parentIndex >>= 1;
                parent = this.heapArray[parentIndex];

                if (!this.IsLessThan(current, parent))
                {
                    break;
                }

                this.indexes[parent] = currentIndex;
                this.heapArray[currentIndex] = parent;

                currentIndex = parentIndex;
            }

            // Put the current item in the right place.
            this.indexes[current] = currentIndex;
            this.heapArray[currentIndex] = current;
        }
    }
}
