// -----------------------------------------------------------------------
// <copyright file="LifoBinaryHeap.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    using System.Collections.Generic;

    using Raquellcesar.Stardew.Common.Utilities;

    /// <summary>
    ///     Implements a binary heap, a heap data structure that takes the form of a binary tree. It
    ///     auto-resizes and treats ties in a LIFO manner.
    /// </summary>
    /// <typeparam name="T">
    ///     The type of the values in the heap. It must be a reference type.
    /// </typeparam>
    public class LifoBinaryHeap<T> : AutoResizableBinaryHeap<T>
        where T : class
    {
        /// <summary>
        ///     An internal dictionary that maps elements to an index representing their insertion
        ///     order. Used to break ties between nodes with the same value.
        /// </summary>
        private readonly Dictionary<T, long> insertionIndexes;

        /// <summary>
        ///     Counts the number of nodes ever added to the heap. Used to record every node order
        ///     of insertion (see <see cref="insertionIndexes"/>).
        /// </summary>
        private long nodeCounter;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LifoBinaryHeap{T}"/> class. It will be
        ///     a min-heap that uses <see cref="Comparer{T}.Default"/> to compare items.
        /// </summary>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize"/>.</param>
        public LifoBinaryHeap(int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(HeapType.Min, null, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="LifoBinaryHeap{T}"/> class. It will use
        ///     <see cref="Comparer{T}.Default"/> to compare items.
        /// </summary>
        /// <param name="heapType">Specifies whether this will be a min or max heap.</param>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize"/>.</param>
        public LifoBinaryHeap(HeapType heapType, int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(heapType, null, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="LifoBinaryHeap{T}"/> class. It will be
        ///     a min-heap.
        /// </summary>
        /// <param name="comparer">The comparer used to compare items.</param>
        /// <param name="capacity">The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize"/>.</param>
        public LifoBinaryHeap(IComparer<T> comparer, int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : this(HeapType.Min, comparer, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="LifoBinaryHeap{T}"/> class.
        /// </summary>
        /// <param name="heapType">Specifies whether this will be a min or max heap.</param>
        /// <param name="comparer">The comparer used to compare items.</param>
        /// <param name="capacity">
        ///     The initial capacity of the heap. Must be greater than 0, defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize"/>.
        /// </param>
        public LifoBinaryHeap(
            HeapType heapType,
            IComparer<T> comparer,
            int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : base(heapType, comparer, capacity)
        {
            this.insertionIndexes = new Dictionary<T, long>(new ObjectReferenceComparer<T>());
            this.nodeCounter = 0;
        }

        /// <summary>
        ///     Implements the abstract function <see
        ///     cref="AutoResizableBinaryHeap{T}.AddInternal(T)"/>. Adds the item to the insertion
        ///     index dictionary (see <see cref="insertionIndexes"/>) and updates the node counter
        ///     <see cref="nodeCounter"/>.
        /// </summary>
        /// <param name="item">The item removed from the heap.</param>
        protected override void AddInternal(T item)
        {
            this.insertionIndexes[item] = this.nodeCounter++;
        }

        /// <summary>
        ///     Implements the abstract function <see
        ///     cref="AutoResizableBinaryHeap{T}.ClearInternal()"/>. Clears the insertion index
        ///     dictionary (see <see cref="insertionIndexes"/>) and resets the node counter <see cref="nodeCounter"/>.
        /// </summary>
        protected override void ClearInternal()
        {
            this.insertionIndexes.Clear();
            this.nodeCounter = 0;
        }

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
        ///     Returns <see langword="true"/> if the first item is less than the second item or
        ///     they are equal but the first was added to the heap first. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        protected override bool IsLessThan(T item1, T item2)
        {
            int cmp = this.Comparer.Compare(item1, item2);
            return cmp < 0 || (cmp == 0 && this.insertionIndexes[item1] > this.insertionIndexes[item2]);
        }

        /// <summary>
        ///     Implements the abstract function <see
        ///     cref="AutoResizableBinaryHeap{T}.RemoveInternal(T)"/>. Removes the item from the
        ///     insertion index dictionary (see <see cref="insertionIndexes"/>).
        /// </summary>
        /// <param name="item">The item added to the heap.</param>
        protected override void RemoveInternal(T item)
        {
            this.insertionIndexes.Remove(item);
        }
    }
}
