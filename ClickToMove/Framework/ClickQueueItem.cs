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
        ///     The tile x coordinate. Kept for efficiency.
        /// </summary>
        private readonly int tileX;

        /// <summary>
        ///     The tile y coordinate. Kept for efficiency.
        /// </summary>
        private readonly int tileY;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ClickQueueItem"/> struct.
        /// </summary>
        /// <param name="mouseX">The mouse x coordinate.</param>
        /// <param name="mouseY">The mouse y coordinate.</param>
        /// <param name="viewportX">The viewport x coordinate.</param>
        /// <param name="viewportY">The viewport y coordinate.</param>
        public ClickQueueItem(int mouseX, int mouseY, int viewportX, int viewportY)
        {
            this.MouseX = mouseX;
            this.MouseY = mouseY;
            this.ViewportX = viewportX;
            this.ViewportY = viewportY;
            this.tileX = mouseX + viewportX;
            this.tileY = mouseY + viewportY;
        }

        /// <summary>
        ///     Gets the mouse x coordinate.
        /// </summary>
        public int MouseX { get; }

        /// <summary>
        ///     Gets the mouse y coordinate.
        /// </summary>
        public int MouseY { get; }

        /// <summary>
        ///     Gets the viewport x coordinate.
        /// </summary>
        public int ViewportX { get; }

        /// <summary>
        ///     Gets the viewport y coordinate.
        /// </summary>
        public int ViewportY { get; }

        /// <inheritdoc/>
        /// <remarks>
        ///     Two clicks are considered equal if they correspond to the same absolute position.
        /// </remarks>
        public bool Equals(ClickQueueItem other)
        {
            return this.tileX == other.tileX && this.tileY == other.tileY;
        }
    }
}
