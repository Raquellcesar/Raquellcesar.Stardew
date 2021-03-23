// -----------------------------------------------------------------------
// <copyright file="Benchmark.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Benchmark.Common.HeapBenchmark
{
    using System;

    using BenchmarkDotNet.Attributes;

    using Raquellcesar.Stardew.Common.DataStructures;
    using Raquellcesar.Stardew.Common.DataStructures.PriorityQueue;

    public class Benchmark
    {
        [Params(1, 100, 10000, 1000000)]
        public int HeapSize;

        private HeapNode[] nodes;

        private FastBinaryHeap<HeapNode> fastBinaryHeap;

        private LifoBinaryHeap<HeapNode> lifoBinaryHeap;

        private PriorityQueueWithSortedDictionary<HeapNode, float> priorityQueueWithSortedDictionary;

        [GlobalSetup]
        public void GlobalSetup()
        {
            Random rng = new Random(34829061);

            this.fastBinaryHeap = new FastBinaryHeap<HeapNode>();
            this.lifoBinaryHeap = new LifoBinaryHeap<HeapNode>();
            this.priorityQueueWithSortedDictionary = new PriorityQueueWithSortedDictionary<HeapNode, float>();

            this.nodes = new HeapNode[this.HeapSize];
            for (int i = 0; i < this.HeapSize; i++)
            {
                int value = rng.Next(16777216);
                this.nodes[i] = new HeapNode(value);
            }
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            this.fastBinaryHeap.Clear();
            this.lifoBinaryHeap.Clear();
            this.priorityQueueWithSortedDictionary.Clear();
        }

        [Benchmark]
        public void PushToFastBinaryHeap()
        {
            for (int i = 0; i < this.HeapSize; i++)
            {
                this.fastBinaryHeap.Push(this.nodes[i]);
            }
        }

        [Benchmark]
        public void PushToLifoBinaryHeap()
        {
            for (int i = 0; i < this.HeapSize; i++)
            {
                this.lifoBinaryHeap.Push(this.nodes[i]);
            }
        }

        [Benchmark]
        public void PushToPriorityQueueWithSortedDictionary()
        {
            for (int i = 0; i < this.HeapSize; i++)
            {
                this.priorityQueueWithSortedDictionary.Enqueue(this.nodes[i], this.nodes[i].Value);
            }
        }

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
        }

        public class PriorityQueueNode
        { }
    }
}