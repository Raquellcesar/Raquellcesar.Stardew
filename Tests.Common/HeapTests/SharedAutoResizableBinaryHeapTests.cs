// -----------------------------------------------------------------------
// <copyright file="SharedAutoResizableBinaryHeapTests.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Tests.Common.HeapTests
{
    using System;

    using NUnit.Framework;

    using Raquellcesar.Stardew.Common.DataStructures;

    public abstract class SharedAutoResizableBinaryHeapTests<THeap> : SharedHeapTests<THeap>
        where THeap : AutoResizableBinaryHeap<HeapNode>
    {
#if DEBUG
        [Test]
        public void TestDebugPopThrowsOnCorruptedHeap()
        {
            HeapNode node1 = new HeapNode(1);
            HeapNode node2 = new HeapNode(2);

            this.Push(node1);
            this.Push(node2);

            node1.Value = 3;

            Assert.Throws<InvalidOperationException>(() => this.Heap.Pop());
        }
#endif

        [Test]
        public void TestHeapAutomaticallyResizes()
        {
            for (int i = 0; i < 1000; i++)
            {
                this.Push(new HeapNode(i));
                Assert.AreEqual(i + 1, this.Heap.Count);
            }

            for (int i = 0; i < 1000; i++)
            {
                HeapNode node = this.Pop();
                Assert.AreEqual(i, node.Value);
            }
        }

        [Test]
        public void TestHeapifyThrowsOnNodeNotInHeap()
        {
            HeapNode node = new HeapNode(this.RandomValue());

            Assert.Throws<InvalidOperationException>(() => this.Heap.Heapify(node));
        }

        [Test]
        public void TestNullParametersThrow()
        {
            Assert.Throws<ArgumentNullException>(() => this.Heap.Contains(null));
            Assert.Throws<ArgumentNullException>(() => this.Heap.Heapify(null));
            Assert.Throws<ArgumentNullException>(() => this.Heap.Push(null));
            Assert.Throws<ArgumentNullException>(() => this.Heap.Remove(null));
        }

        [Test]
        public void TestPopThrowsOnEmptyHeap()
        {
            Assert.Throws<InvalidOperationException>(() => this.Heap.Pop());
        }

        [Test]
        public void TestPushThrowsOnAlreadyPushedNode()
        {
            HeapNode node = new HeapNode(this.RandomValue());

            this.Push(node);

            Assert.Throws<InvalidOperationException>(() => this.Heap.Push(node));
        }

        [Test]
        public void TestRemoveThrowsOnNodeNotInHeap()
        {
            HeapNode node = new HeapNode(this.RandomValue());

            Assert.Throws<InvalidOperationException>(() => this.Heap.Remove(node));
        }

        protected abstract override THeap CreateHeap();

        protected abstract override bool IsValidHeap();
    }
}