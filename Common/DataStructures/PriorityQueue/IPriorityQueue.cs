// -----------------------------------------------------------------------
// <copyright file="IPriorityQueue.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    /// <summary>
    ///     The interface to implement for a generic priority queue structure. A priority queue is
    ///     an abstract data type similar to a regular queue data structure in which each element
    ///     additionally has a "priority" associated with it. In a priority queue, an element with
    ///     high priority is served before an element with low priority.
    /// </summary>
    /// <remarks>
    ///     For speed purposes, it is recommended that you *don't* access the priority queue through
    ///     this interface, since the JIT can (theoretically?) optimize method calls from
    ///     concrete-types slightly better.
    /// </remarks>
    /// <typeparam name="TItem">
    ///     The type of elements in the priority queue. It must be a reference type.
    /// </typeparam>
    /// <typeparam name="TPriority">The type for the priority value.</typeparam>
    public interface IPriorityQueue<TItem, TPriority>
        where TItem : class
    {
        /// <summary>
        ///     Gets the number of elements in the priority queue.
        /// </summary>
        int Count { get; }

        /// <summary>
        ///     Removes every element from the priority queue.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Returns whether the given item is in the priority queue with the given priority.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <param name="priority">The priority to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given item is in the priority queue, <see
        ///     langword="false"/> otherwise.
        /// </returns>
        bool Contains(TItem item, TPriority priority);

        /// <summary>
        ///     Returns the element with highest priority, after removing it from the priority queue.
        /// </summary>
        /// <returns>A T object.</returns>
        TItem Dequeue();

        /// <summary>
        ///     Adds a new item to the priority queue.
        /// </summary>
        /// <param name="item">The item to insert.</param>
        /// <param name="priority">The priority of the item to add.</param>
        void Enqueue(TItem item, TPriority priority);

        /// <summary>
        ///     Checks if the priority queue is empty.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the priority queue is empty, <see
        ///     langword="false"/> otherwise.
        /// </returns>
        bool IsEmpty();

        /// <summary>
        ///     Returns the item with highest priority, without removing it (use <see
        ///     cref="Dequeue"/> for that).
        /// </summary>
        /// <returns>A T object.</returns>
        TItem Peek();
    }
}
