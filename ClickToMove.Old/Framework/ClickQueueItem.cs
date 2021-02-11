// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickQueueItem.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;

    public readonly struct ClickQueueItem : IEquatable<ClickQueueItem>
    {
        public int MouseX { get; }

        public int MouseY { get; }

        public int ViewportX { get; }

        public int ViewportY { get; }

        public int TileX { get; }

        public int TileY { get; }

        public ClickQueueItem(int mouseX, int mouseY, int viewportX, int viewportY, int tileX, int tileY)
        {
            this.MouseX = mouseX;
            this.MouseY = mouseY;
            this.ViewportX = viewportX;
            this.ViewportY = viewportY;
            this.TileX = tileX;
            this.TileY = tileY;
        }

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