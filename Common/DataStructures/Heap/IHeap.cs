// -----------------------------------------------------------------------
// <copyright file="IHeap.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    using System.Collections.Generic;

    /// <summary>
    ///     The interface to implement for a generic heap data structure. A heap is a specialized
    ///     tree-based data structure which is essentially an almost complete tree that satisfies
    ///     the heap property: in a max heap, for any given node C, if P is a parent node of C, then
    ///     the key (the value) of P is greater than or equal to the key of C. In a min heap, the
    ///     key of P is less than or equal to the key of C.
    /// </summary>
    /// <remarks>
    ///     For speed purposes, it is recommended that you *don't* access the Heap through this
    ///     interface, since the JIT can (theoretically?) optimize method calls from concrete-types
    ///     slightly better.
    /// </remarks>
    /// <typeparam name="T">The type of elements in the heap.</typeparam>
    public interface IHeap<T> : IEnumerable<T>
    {
        /// <summary>
        ///     Gets the number of elements in the heap.
        /// </summary>
        int Count { get; }

        /// <summary>
        ///     Removes every element from the heap.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Returns whether the given item is in the heap.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given item is in the heap, <see
        ///     langword="false"/> otherwise.
        /// </returns>
        bool Contains(T item);

        /// <summary>
        ///     Call this method after an item in the heap changes to restore heap condition.
        /// </summary>
        /// <param name="item">The item that needs to be moved up or down the heap.</param>
        void Heapify(T item);

        /// <summary>
        ///     Checks if the heap is empty.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the heap is empty, <see langword="false"/> otherwise.
        /// </returns>
        bool IsEmpty();

        /// <summary>
        ///     Checks to make sure the heap is in a valid state, i.e. it satisfies the heap
        ///     property. Used for testing/debugging the heap.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the heap satisfies the heap property, <see
        ///     langword="false"/> otherwise.
        /// </returns>
        bool IsValid();

        /// <summary>
        ///     Returns the item at the root node, without removing it (use <see cref="Pop"/> for
        ///     that). This will be the maximum item in a max-heap, or the minimum item in a min-heap.
        /// </summary>
        /// <returns>A T object.</returns>
        T Peek();

        /// <summary>
        ///     Returns the element at the root node, i.e. the node at the "top" of the heap (with
        ///     no parents), after removing it from the heap. This will be the maximum item in a
        ///     max-heap, or the minimum item in a min-heap.
        /// </summary>
        /// <returns>A T object.</returns>
        T Pop();

        /// <summary>
        ///     Adds a new item to the heap.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        void Push(T item);

        /// <summary>
        ///     Removes an item from the heap. The item does not need to be the root of the heap.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        void Remove(T item);

        /// <summary>
        ///     Removes an item from the heap and pushes a new item. More efficient than <see
        ///     cref="Remove(T)"/> followed by <see cref="Push(T)"/> (or vice versa), since we only
        ///     need to balance once, not twice.
        /// </summary>
        /// <param name="item">The item to remove from the heap.</param>
        /// <param name="newItem">The item to add to the heap.</param>
        void Replace(T item, T newItem);
    }
}
