// -----------------------------------------------------------------------
// <copyright file="FastBinaryHeapTests.cs" company="Raquellcesar">
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
    internal class FastBinaryHeapTests : SharedAutoResizableBinaryHeapTests<FastBinaryHeap<HeapNode>>
    {
        protected override FastBinaryHeap<HeapNode> CreateHeap()
        {
            return new FastBinaryHeap<HeapNode>();
        }

        protected override bool IsValidHeap()
        {
            return this.Heap.IsValid();
        }
    }
}