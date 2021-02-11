// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="FastBinaryHeapTests.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

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