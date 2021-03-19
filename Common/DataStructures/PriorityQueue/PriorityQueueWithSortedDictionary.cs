// -----------------------------------------------------------------------
// <copyright file="PriorityQueueWithSortedDictionary.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures.PriorityQueue
{
    using System.Collections.Generic;

    /// <summary>
    ///     A priority queue implemented with a sorted dictionary (see <see cref="IPriorityQueue{TItem,TPriority}" />.
    ///     It treats ties in a FIFO manner.
    /// </summary>
    /// <typeparam name="TItem">
    ///     The type of elements in the priority queue. It must be a reference type.
    /// </typeparam>
    /// <typeparam name="TPriority">
    ///     The type for the priority value.
    /// </typeparam>
    public class PriorityQueueWithSortedDictionary<TItem, TPriority> : IPriorityQueue<TItem, TPriority>
        where TItem : class
    {
        /// <summary>
        ///     The internal dictionary used to store the priority queue elements.
        /// </summary>
        private readonly SortedDictionary<TPriority, Queue<TItem>> items;

        /// <summary>
        ///     Backing field for the <see cref="Count" /> property.
        /// </summary>
        private int count;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueueWithSortedDictionary{TItem,TPriority}" /> class.
        /// </summary>
        public PriorityQueueWithSortedDictionary()
        {
            this.items = new SortedDictionary<TPriority, Queue<TItem>>();
            this.count = 0;
        }

        /// <inheritdoc />
        public int Count => this.count;

        /// <inheritdoc />
        public void Clear()
        {
            foreach (KeyValuePair<TPriority, Queue<TItem>> item in this.items)
            {
                item.Value.Clear();
            }

            this.count = 0;
        }

        /// <inheritdoc />
        /// >
        public bool Contains(TItem item, TPriority priority)
        {
            return this.items.TryGetValue(priority, out Queue<TItem> itemsQueue) && itemsQueue.Contains(item);
        }

        /// <inheritdoc />
        /// >
        public TItem Dequeue()
        {
            if (this.count > 0)
            {
                foreach (Queue<TItem> itemsQueue in this.items.Values)
                {
                    if (itemsQueue.Count > 0)
                    {
                        this.count--;
                        return itemsQueue.Dequeue();
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        /// >
        public void Enqueue(TItem item, TPriority priority)
        {
            if (!this.items.ContainsKey(priority))
            {
                this.items.Add(priority, new Queue<TItem>());
            }

            this.items[priority].Enqueue(item);
            this.count++;
        }

        /// <inheritdoc />
        public bool IsEmpty()
        {
            return this.count == 0;
        }

        /// <inheritdoc />
        /// >
        public TItem Peek()
        {
            if (this.count > 0)
            {
                foreach (Queue<TItem> itemsQueue in this.items.Values)
                {
                    if (itemsQueue.Count > 0)
                    {
                        return itemsQueue.Peek();
                    }
                }
            }

            return null;
        }
    }
}