// -----------------------------------------------------------------------
// <copyright file="LifoBinaryHeapTests.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Tests.Common.HeapTests
{
    using NUnit.Framework;

    using Raquellcesar.Stardew.Common.DataStructures;

    [TestFixture]
    internal class LifoBinaryHeapTests : SharedAutoResizableBinaryHeapTests<LifoBinaryHeap<HeapNode>>
    {
        [Test]
        public void TestLifoOrderOnTies()
        {
            int value = this.RandomValue();

            int num = this.Rng.Next(1, 10);
            HeapNode[] nodes = new HeapNode[num];
            for (int i = 0; i < num; i++)
            {
                nodes[i] = new HeapNode(value);
                this.Heap.Push(nodes[i]);
                Assert.AreSame(nodes[i], this.Heap.Peek());
            }

            for (int i = num - 1; i >= 0; i--)
            {
                Assert.AreSame(nodes[i], this.Heap.Pop());
            }
        }

        protected override LifoBinaryHeap<HeapNode> CreateHeap()
        {
            return new LifoBinaryHeap<HeapNode>();
        }

        protected override bool IsValidHeap()
        {
            return this.Heap.IsValid();
        }
    }
}