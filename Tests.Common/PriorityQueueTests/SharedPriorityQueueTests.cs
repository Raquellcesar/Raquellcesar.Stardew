// -----------------------------------------------------------------------
// <copyright file="SharedPriorityQueueTests.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Tests.Common.PriorityQueueTests
{
    using System;

    using NUnit.Framework;

    using Raquellcesar.Stardew.Common.DataStructures;

    public class PriorityQueueNode
    {
    }

    public abstract class SharedPriorityQueueTests<TPriorityQueue>
        where TPriorityQueue : IPriorityQueue<PriorityQueueNode, int>
    {
        protected TPriorityQueue PriorityQueue { get; set; }

        protected Random Rng { get; } = new Random(34829061);

        [SetUp]
        public void SetUp()
        {
            this.PriorityQueue = this.CreatePriorityQueue();
        }

        [Test]
        public void TestClear()
        {
            // Clearing a newly created priority queue has no effect.
            this.PriorityQueue.Clear();
            Assert.AreEqual(0, this.PriorityQueue.Count);

            // Clearing a non empty priority queue should remove all its elements.
            int num = this.Rng.Next(1, 10);
            for (int i = 0; i < num; i++)
            {
                this.PriorityQueue.Enqueue(new PriorityQueueNode(), this.RandomValue());
            }

            this.PriorityQueue.Clear();
            Assert.AreEqual(0, this.PriorityQueue.Count);
        }

        [Test]
        public void TestContains()
        {
            PriorityQueueNode node1 = new PriorityQueueNode();
            PriorityQueueNode node2 = new PriorityQueueNode();
            PriorityQueueNode node3 = new PriorityQueueNode();
            int priority1 = this.RandomValue();
            int priority2 = this.RandomValue();

            // A newly created priority queue contains no elements.
            Assert.IsFalse(this.PriorityQueue.Contains(node1, priority1));

            // Once a node is added to the priority queue with a certain priority, it's contained in it
            // but only for the specified priority.
            this.PriorityQueue.Enqueue(node1, priority1);
            Assert.IsTrue(this.PriorityQueue.Contains(node1, priority1));
            Assert.IsFalse(this.PriorityQueue.Contains(node1, priority2));

            this.PriorityQueue.Enqueue(node2, priority2);
            Assert.IsTrue(this.PriorityQueue.Contains(node1, priority1));
            Assert.IsFalse(this.PriorityQueue.Contains(node1, priority2));
            Assert.IsTrue(this.PriorityQueue.Contains(node2, priority2));
            Assert.IsFalse(this.PriorityQueue.Contains(node2, priority1));

            this.PriorityQueue.Enqueue(node3, priority1);
            Assert.IsTrue(this.PriorityQueue.Contains(node1, priority1));
            Assert.IsFalse(this.PriorityQueue.Contains(node1, priority2));
            Assert.IsTrue(this.PriorityQueue.Contains(node2, priority2));
            Assert.IsFalse(this.PriorityQueue.Contains(node2, priority1));
            Assert.IsTrue(this.PriorityQueue.Contains(node3, priority1));
            Assert.IsFalse(this.PriorityQueue.Contains(node3, priority2));
        }

        [Test]
        public void TestCount()
        {
            // A newly created priority queue has 0 elements.
            Assert.AreEqual(0, this.PriorityQueue.Count);

            // Enqueueing an element to the priority queue increases the number of elements by 1.
            int numEnqueued = this.Rng.Next(1, 10);
            for (int i = 0; i < numEnqueued; i++)
            {
                this.PriorityQueue.Enqueue(new PriorityQueueNode(), this.RandomValue());
            }

            Assert.AreEqual(numEnqueued, this.PriorityQueue.Count);

            // Dequeueing an element from the priority queue decreases the number of elements by 1.
            int numDequeued = this.Rng.Next(1, numEnqueued);
            for (int i = 0; i < numDequeued; i++)
            {
                this.PriorityQueue.Dequeue();
            }

            Assert.AreEqual(numEnqueued - numDequeued, this.PriorityQueue.Count);
        }

        [Test]
        public void TestDequeue()
        {
            PriorityQueueNode node1 = new PriorityQueueNode();
            PriorityQueueNode node2 = new PriorityQueueNode();
            PriorityQueueNode node3 = new PriorityQueueNode();
            int priority = this.RandomValue();

            this.PriorityQueue.Enqueue(node1, priority);
            this.PriorityQueue.Enqueue(node2, priority + 1);
            Assert.AreEqual(node1, this.PriorityQueue.Dequeue());

            this.PriorityQueue.Enqueue(node3, priority + 1);
            Assert.AreEqual(node2, this.PriorityQueue.Dequeue());
        }

        [Test]
        public void TestIsEmpty()
        {
            Assert.AreEqual(true, this.PriorityQueue.IsEmpty());

            this.PriorityQueue.Enqueue(new PriorityQueueNode(), this.RandomValue());

            Assert.AreEqual(false, this.PriorityQueue.IsEmpty());
        }

        [Test]
        public void TestPeek()
        {
            PriorityQueueNode node1 = new PriorityQueueNode();
            PriorityQueueNode node2 = new PriorityQueueNode();
            PriorityQueueNode node3 = new PriorityQueueNode();
            int priority = this.RandomValue();

            this.PriorityQueue.Enqueue(node1, priority);
            Assert.AreEqual(node1, this.PriorityQueue.Peek());

            this.PriorityQueue.Enqueue(node2, priority - 1);
            Assert.AreEqual(node2, this.PriorityQueue.Peek());

            this.PriorityQueue.Enqueue(node3, priority - 1);
            Assert.AreEqual(node2, this.PriorityQueue.Peek());
        }

        protected abstract TPriorityQueue CreatePriorityQueue();

        protected int RandomValue()
        {
            // Constrain to range float can hold with no rounding.
            return this.Rng.Next(16777216);
        }
    }
}