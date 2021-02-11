// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="PriorityQueueWithSortedDictionaryTests.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.Tests.Common.PriorityQueueTests
{
    using NUnit.Framework;

    using Raquellcesar.Stardew.Common.DataStructures.PriorityQueue;

    [TestFixture]
    internal class PriorityQueueWithSortedDictionaryTests :
        SharedPriorityQueueTests<PriorityQueueWithSortedDictionary<PriorityQueueNode, int>>
    {
        protected override PriorityQueueWithSortedDictionary<PriorityQueueNode, int> CreatePriorityQueue()
        {
            return new PriorityQueueWithSortedDictionary<PriorityQueueNode, int>();
        }
    }
}