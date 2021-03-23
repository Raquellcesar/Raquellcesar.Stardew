// -----------------------------------------------------------------------
// <copyright file="ClickQueueItem.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;

    public readonly struct ClickQueueItem : IEquatable<ClickQueueItem>
    {
        public ClickQueueItem(int mouseX, int mouseY, int viewportX, int viewportY, int tileX, int tileY)
        {
            this.MouseX = mouseX;
            this.MouseY = mouseY;
            this.ViewportX = viewportX;
            this.ViewportY = viewportY;
            this.TileX = tileX;
            this.TileY = tileY;
        }

        public int MouseX { get; }

        public int MouseY { get; }

        public int TileX { get; }

        public int TileY { get; }

        public int ViewportX { get; }

        public int ViewportY { get; }

        public bool Equals(ClickQueueItem other)
        {
            return this.TileX == other.TileX && this.TileY == other.TileY;
        }

        public override bool Equals(object obj)
        {
            return obj is ClickQueueItem other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (this.TileX * 397) ^ this.TileY;
            }
        }
    }
}
