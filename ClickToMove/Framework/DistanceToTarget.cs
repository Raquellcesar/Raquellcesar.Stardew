// -----------------------------------------------------------------------
// <copyright file="DistanceToTarget.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using StardewValley;

    /// <summary>
    ///     Represents the distance to the target while the <see cref="Farmer"/> is in the final
    ///     tile of the path.
    /// </summary>
    public enum DistanceToTarget
    {
        /// <summary>
        ///     The distance to the target is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        ///     The target is too close. The Farmer will need to move away from it.
        /// </summary>
        TooClose,

        /// <summary>
        ///     The target is too far. The Farmer will eventually need to get closer.
        /// </summary>
        TooFar,
    }
}
