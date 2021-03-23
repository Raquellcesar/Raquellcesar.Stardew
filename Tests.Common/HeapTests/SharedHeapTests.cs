// -----------------------------------------------------------------------
// <copyright file="SharedHeapTests.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Tests.Common.HeapTests
{
    using System;
    using System.Globalization;

    using NUnit.Framework;

    using Raquellcesar.Stardew.Common.DataStructures;

    public class HeapNode : IComparable<HeapNode>
    {
        public HeapNode(float value)
        {
            this.Value = value;
        }

        public float Value { get; protected internal set; }

        public int CompareTo(HeapNode other)
        {
            return this.Value.CompareTo(other.Value);
        }

        public override string ToString()
        {
            return $"Value: {this.Value.ToString(CultureInfo.CurrentCulture)}";
        }
    }

    public abstract class SharedHeapTests<THeap>
        where THeap : AutoResizableBinaryHeap<HeapNode>
    {
        protected Random Rng { get; } = new Random(34829061);

        protected THeap Heap { get; set; }

        [SetUp]
        public void SetUp()
        {
            this.Heap = this.CreateHeap();
        }

        [Test]
        public void TestClear()
        {
            // Clearing a newly created heap has no effect.
            this.Heap.Clear();
            Assert.AreEqual(0, this.Heap.Count);

            // Clearing a non empty heap should remove all its elements.
            int num = this.Rng.Next(1, 10);
            for (int i = 0; i < num; i++)
            {
                this.Push(new HeapNode(this.RandomValue()));
            }

            this.Heap.Clear();
            Assert.AreEqual(0, this.Heap.Count);
        }

        [Test]
        public void TestContains()
        {
            HeapNode node1 = new HeapNode(this.RandomValue());
            HeapNode node2 = new HeapNode(this.RandomValue());

            // A newly created heap contains no elements.
            Assert.IsFalse(this.Heap.Contains(node1));

            // Once a node is pushed to the heap, it's contained in it.
            this.Push(node1);
            Assert.IsTrue(this.Heap.Contains(node1));

            this.Push(node2);
            Assert.IsTrue(this.Heap.Contains(node1));
            Assert.IsTrue(this.Heap.Contains(node2));
        }

        [Test]
        public void TestCount()
        {
            // A newly created heap has 0 elements.
            Assert.AreEqual(0, this.Heap.Count);

            // Pushing an element to the heap increases the number of elements by 1.
            int numPushed = this.Rng.Next(1, 10);
            for (int i = 0; i < numPushed; i++)
            {
                this.Push(new HeapNode(this.RandomValue()));
            }

            // Popping an element from the heap decreases the number of elements by 1.
            int numPopped = this.Rng.Next(1, numPushed);
            for (int i = 0; i < numPopped; i++)
            {
                this.Pop();
            }

            Assert.AreEqual(numPushed - numPopped, this.Heap.Count);
        }

        [Test]
        public void TestHeapify()
        {
            int num = this.Rng.Next(1, 10);
            HeapNode[] nodes = new HeapNode[num];
            int min = int.MaxValue;
            for (int i = 0; i < num; i++)
            {
                nodes[i] = new HeapNode(this.RandomValue());
                this.Push(nodes[i]);
                if (nodes[i].Value < min)
                {
                    min = (int)nodes[i].Value;
                }
            }

            HeapNode node = nodes[this.Rng.Next(0, num)];
            node.Value = min - 1;

            this.Heap.Heapify(node);
            Assert.AreEqual(node, this.Pop());
        }

        [Test]
        public void TestIsEmpty()
        {
            Assert.AreEqual(true, this.Heap.IsEmpty());

            this.Heap.Push(new HeapNode(this.RandomValue()));

            Assert.AreEqual(false, this.Heap.IsEmpty());
        }

        [Test]
        public void TestPeek()
        {
            int value = this.RandomValue();
            HeapNode node1 = new HeapNode(value);

            this.Heap.Push(node1);
            Assert.AreEqual(node1, this.Heap.Peek());

            HeapNode node2 = new HeapNode(value - 1);
            this.Heap.Push(node2);
            Assert.AreEqual(node2, this.Heap.Peek());

            HeapNode node3 = new HeapNode(value + 1);
            this.Heap.Push(node3);
            Assert.AreEqual(node2, this.Heap.Peek());
        }

        [Test]
        public void TestPop()
        {
            int value = this.RandomValue();
            HeapNode node1 = new HeapNode(value);

            this.Heap.Push(node1);
            Assert.AreEqual(node1, this.Heap.Pop());

            HeapNode node2 = new HeapNode(value - 1);
            this.Heap.Push(node1);
            this.Heap.Push(node2);
            Assert.AreEqual(node2, this.Heap.Pop());

            HeapNode node3 = new HeapNode(value + 1);
            this.Heap.Push(node3);
            Assert.AreEqual(node1, this.Heap.Pop());
        }

        [Test]
        public void TestReplace()
        {
            int num = this.Rng.Next(1, 10);
            HeapNode[] nodes = new HeapNode[num];
            for (int i = 0; i < num; i++)
            {
                nodes[i] = new HeapNode(this.RandomValue());
                this.Push(nodes[i]);
            }

            HeapNode node = nodes[this.Rng.Next(0, num)];
            HeapNode newNode = new HeapNode(this.RandomValue());

            this.Heap.Replace(node, newNode);
            Assert.IsFalse(this.Heap.Contains(node));
            Assert.IsTrue(this.Heap.Contains(newNode));
            Assert.AreEqual(num, this.Heap.Count);
            Assert.IsTrue(this.IsValidHeap());
        }

        [Test]
        public void TestRemove()
        {
            int num = this.Rng.Next(1, 10);
            HeapNode[] nodes = new HeapNode[num];
            for (int i = 0; i < num; i++)
            {
                nodes[i] = new HeapNode(this.RandomValue());
                this.Push(nodes[i]);
            }

            HeapNode node = nodes[this.Rng.Next(0, num)];

            this.Heap.Remove(node);
            Assert.IsFalse(this.Heap.Contains(node));
            Assert.AreEqual(num - 1, this.Heap.Count);
            Assert.IsTrue(this.IsValidHeap());
        }

        protected abstract THeap CreateHeap();

        protected abstract bool IsValidHeap();

        protected HeapNode Pop()
        {
            HeapNode node = this.Heap.Pop();
            Assert.IsTrue(this.IsValidHeap());
            return node;
        }

        protected void Push(HeapNode node)
        {
            this.Heap.Push(node);
            Assert.IsTrue(this.IsValidHeap());
        }

        protected int RandomValue()
        {
            // Constrain to range float can hold with no rounding.
            return this.Rng.Next(16777216);
        }
    }
}