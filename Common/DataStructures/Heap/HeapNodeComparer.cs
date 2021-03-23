// -----------------------------------------------------------------------
// <copyright file="HeapNodeComparer.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.DataStructures
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     A class that wraps a given comparer in order to compare objects in a heap according to the heap type.
    ///     This allows the direct use of object comparisons by the heap, without the need of permanently testing for heap
    ///     type.
    /// </summary>
    /// <typeparam name="T">The type of objects to compare.</typeparam>
    internal class HeapNodeComparer<T> : IComparer<T>
    {
        /// <summary>
        ///     The comparer function object, defined on instantiation of this comparer depending on the heap type parameter.
        ///     This function will be invoked by the <see cref="Compare(T, T)" /> method.
        /// </summary>
        private readonly Func<T, T, int> comparerFunction;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HeapNodeComparer{T}"/> class.
        /// </summary>
        /// <param name="comparer">
        /// The object comparer.
        /// </param>
        /// <param name="heapType">
        /// The heap type, either min or max. It defaults to <see cref="HeapType.Min"/>.
        /// </param>
        internal HeapNodeComparer(IComparer<T> comparer, HeapType heapType = HeapType.Min)
        {
            if (heapType == HeapType.Min)
            {
                this.comparerFunction = comparer.Compare;
            }
            else
            {
                this.comparerFunction = (x, y) => -comparer.Compare(x, y);
            }
        }

        /// <summary>
        ///     Compares two objects according to the heap type.
        /// </summary>
        /// <remarks>This method will invoke the delegate <see cref="comparerFunction" />, created on instantiation.</remarks>
        /// <param name="x">First object to compare.</param>
        /// <param name="y">Second object to compare.</param>
        /// <returns>
        ///     A signed integer that indicates the relative values of x and y, as shown in the following table.
        ///     <list type="table">
        ///         <listheader>
        ///             <term>Value</term>
        ///             <description>Meaning</description>
        ///         </listheader>
        ///         <item>
        ///             <term>Less than zero</term>
        ///             <description>x is less than y, if the heap is a min-heap; x is greater than y otherwise.</description>
        ///         </item>
        ///         <item>
        ///             <term>0</term>
        ///             <description>x equals y.</description>
        ///         </item>
        ///         <item>
        ///             <term>Greater than zero</term>
        ///             <description>x is greater than y, if the heap is a max-heap; x is less than y otherwise.</description>
        ///         </item>
        ///     </list>
        /// </returns>
        public int Compare(T x, T y)
        {
            return this.comparerFunction.Invoke(x, y);
        }
    }
}