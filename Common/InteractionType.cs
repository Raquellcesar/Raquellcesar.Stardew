// -----------------------------------------------------------------------
// <copyright file="InteractionType.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common
{
    /// <summary>
    ///     The interaction available to the farmer at a clicked tile.
    /// </summary>
    public enum InteractionType
    {
        /// <summary>
        ///     No interaction available.
        /// </summary>
        None = 0,

        /// <summary>
        ///     The tile is actionable.
        /// </summary>
        Action = 1,

        /// <summary>
        ///     The tile can be talked to.
        /// </summary>
        Speech = 2,

        /// <summary>
        ///     The tile can be inspected.
        /// </summary>
        Inspection = 3,

        /// <summary>
        ///     The tile can be harvested.
        /// </summary>
        Harvest = 4,
    }
}
