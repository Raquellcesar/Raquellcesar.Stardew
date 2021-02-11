// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ObjectReferenceComparer.cs">
//     Copyright (c) 2021 Raquellcesar
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common.Utilities
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    ///     A generic comparer that considers two references equal if they point to the same instance.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class ObjectReferenceComparer<T> : IEqualityComparer<T>
    {
        /// <summary>
        ///     Determines whether the specified objects are equal.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the specified objects are equal; otherwise, false.
        /// </returns>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        ///     Get a hash code for the specified object.
        /// </summary>
        /// <param name="obj">The object to get the hash code for.</param>
        /// <returns>The hash code for the given object.</returns>
        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
