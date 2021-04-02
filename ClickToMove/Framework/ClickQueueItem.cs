// ------------------------------------------------------------------------------------------------
// <copyright file="ClickQueueItem.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;

    /// <summary>
    ///     Represents a mouse click that can be queued. The struct keeps information about the
    ///     position clicked.
    /// </summary>
    public readonly struct ClickQueueItem : IEquatable<ClickQueueItem>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ClickQueueItem"/> struct.
        /// </summary>
        /// <param name="x">The clicked x absolute coordinate.</param>
        /// <param name="y">The clicked y absolute coordinate.</param>
        public ClickQueueItem(int x, int y)
        {
            this.ClickX = x;
            this.ClickY = y;
        }

        /// <summary>
        ///     Gets the clicked point x absolute coordinate.
        /// </summary>
        public int ClickX { get; }

        /// <summary>
        ///     Gets the clicked point y absolute coordinate.
        /// </summary>
        public int ClickY { get; }

        /// <inheritdoc/>
        /// <remarks>
        ///     Two clicks are considered equal if they correspond to the same absolute position.
        /// </remarks>
        public bool Equals(ClickQueueItem other)
        {
            return this.ClickX == other.ClickX && this.ClickY == other.ClickY;
        }
    }
}
