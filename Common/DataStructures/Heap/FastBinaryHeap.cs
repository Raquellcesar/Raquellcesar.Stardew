// -----------------------------------------------------------------------
// <copyright file="FastBinaryHeap.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    using System.Collections.Generic;

    /// <inheritdoc />
    public class FastBinaryHeap<T> : AutoResizableBinaryHeap<T>
        where T : class
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FastBinaryHeap{T}" /> class.
        ///     It will be a min-heap that uses <see cref="Comparer{T}.Default" /> to compare items.
        /// </summary>
        /// <param name="capacity">
        ///     The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize" />.
        /// </param>
        public FastBinaryHeap(int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : base(capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FastBinaryHeap{T}" /> class.
        ///     It will use <see cref="Comparer{T}.Default" /> to compare items.
        /// </summary>
        /// <param name="heapType">
        ///     Specifies whether this will be a min or max heap.
        /// </param>
        /// <param name="capacity">
        ///     The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize" />.
        /// </param>
        public FastBinaryHeap(HeapType heapType, int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : base(heapType, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FastBinaryHeap{T}" /> class.
        ///     It will be a min-heap.
        /// </summary>
        /// <param name="comparer">
        ///     The comparer used to compare items.
        /// </param>
        /// <param name="capacity">
        ///     The initial capacity of the heap. It defaults to <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize" />.
        /// </param>
        public FastBinaryHeap(IComparer<T> comparer, int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : base(comparer, capacity)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="FastBinaryHeap{T}" /> class.
        /// </summary>
        /// <param name="heapType">
        ///     Specifies whether this will be a min or max heap.
        /// </param>
        /// <param name="comparer">
        ///     The comparer used to compare items.
        /// </param>
        /// <param name="capacity">
        ///     The initial capacity of the heap. Must be greater than 0, defaults to
        ///     <see cref="AutoResizableBinaryHeap{T}.InitialHeapSize" />.
        /// </param>
        public FastBinaryHeap(
            HeapType heapType,
            IComparer<T> comparer,
            int capacity = AutoResizableBinaryHeap<T>.InitialHeapSize)
            : base(heapType, comparer, capacity)
        {
        }

        /// <summary>
        ///     Implements the abstract function <see cref="AutoResizableBinaryHeap{T}.AddInternal(T)" />.
        ///     This function body is empty.
        /// </summary>
        /// <param name="item">The item removed from the heap.</param>
        protected override void AddInternal(T item)
        {
        }

        /// <summary>
        ///     Implements the abstract function <see cref="AutoResizableBinaryHeap{T}.ClearInternal()" />.
        ///     This function body is empty.
        /// </summary>
        protected override void ClearInternal()
        {
        }

        /// <summary>
        ///     Implements the abstract function <see cref="AutoResizableBinaryHeap{T}.RemoveInternal(T)" />.
        ///     This function body is empty.
        /// </summary>
        /// <param name="item">The item added to the heap.</param>
        protected override void RemoveInternal(T item)
        {
        }
    }
}