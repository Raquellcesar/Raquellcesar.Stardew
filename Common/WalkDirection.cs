// -----------------------------------------------------------------------
// <copyright file="WalkDirection.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.Common
{
    using System;

    using Microsoft.Xna.Framework;

    /// <summary>
    ///     This class represents the movement directions allowed in the game. The <see cref="int"/>
    ///     values used for the directions must be inline with the values used for facing directions
    ///     in game.
    /// </summary>
    public sealed class WalkDirection
    {
        /// <summary>
        ///     No direction at all.
        /// </summary>
        public static readonly WalkDirection None = new WalkDirection("None", -1, 0, 0);

        /// <summary>
        ///     The up direction.
        /// </summary>
        public static readonly WalkDirection Up = new WalkDirection("Up", 0, 0, -1);

        /// <summary>
        ///     The diagonal up right direction.
        /// </summary>
        public static readonly WalkDirection UpRight = new WalkDirection("UpRight", 4, 1, -1);

        /// <summary>
        ///     The right direction.
        /// </summary>
        public static readonly WalkDirection Right = new WalkDirection("Right", 1, 1, 0);

        /// <summary>
        ///     The diagonal up left direction.
        /// </summary>
        public static readonly WalkDirection DownRight = new WalkDirection("DownRight", 6, 1, 1);

        /// <summary>
        ///     The down direction.
        /// </summary>
        public static readonly WalkDirection Down = new WalkDirection("Down", 2, 0, 1);

        /// <summary>
        ///     The diagonal up left direction.
        /// </summary>
        public static readonly WalkDirection DownLeft = new WalkDirection("DownLeft", 7, -1, 1);

        /// <summary>
        ///     The left direction.
        /// </summary>
        public static readonly WalkDirection Left = new WalkDirection("Left", 3, -1, 0);

        /// <summary>
        ///     The diagonal up left direction.
        /// </summary>
        public static readonly WalkDirection UpLeft = new WalkDirection("UpLeft", 5, -1, -1);

        /// <summary>
        ///     The known directions.
        /// </summary>
        public static readonly WalkDirection[] Directions =
            {
                WalkDirection.Up, WalkDirection.Right, WalkDirection.Down, WalkDirection.Left, WalkDirection.UpRight,
                WalkDirection.UpLeft, WalkDirection.DownRight, WalkDirection.DownLeft,
            };

        /// <summary>
        ///     The vertical and horizontal directions.
        /// </summary>
        public static readonly WalkDirection[] SimpleDirections =
            {
                WalkDirection.Up, WalkDirection.Right, WalkDirection.Down, WalkDirection.Left,
            };

        /// <summary>
        ///     The diagonal directions.
        /// </summary>
        public static readonly WalkDirection[] DiagonalDirections =
            {
                WalkDirection.UpRight, WalkDirection.UpLeft, WalkDirection.DownRight, WalkDirection.DownLeft,
            };

        /// <summary>
        ///     Initializes a new instance of the <see cref="WalkDirection"/> class.
        /// </summary>
        /// <param name="name">The instance display name.</param>
        /// <param name="value">The instance value.</param>
        /// <param name="x">The x coordinate of the direction vector represented by this instance.</param>
        /// <param name="y">The y coordinate of the direction vector represented by this instance.</param>
        private WalkDirection(string name, int value, int x, int y)
        {
            this.Name = name;
            this.Value = value;
            this.X = x;
            this.Y = y;
        }

        /// <summary>
        ///     Gets the <see cref="WalkDirection"/> name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the <see cref="WalkDirection"/> value.
        /// </summary>
        public int Value { get; }

        /// <summary>
        ///     Gets the x coordinate of the direction vector represented by this <see cref="WalkDirection"/>.
        /// </summary>
        public int X { get; }

        /// <summary>
        ///     Gets the x coordinate of the direction vector represented by this <see cref="WalkDirection"/>.
        /// </summary>
        public int Y { get; }

        /// <summary>
        ///     Finds the facing direction when getting from a start point to a target point.
        /// </summary>
        /// <remarks>
        ///     This only works correctly if the int values used for the walk directions are in sync
        ///     with the values used for facing directions in game.
        /// </remarks>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="startPosition">The start position.</param>
        /// <returns>The <see langword="int"/> representing the facing direction when going from start to target.</returns>
        public static int GetFacingDirection(Vector2 targetPosition, Vector2 startPosition)
        {
            return WalkDirection.GetFacingWalkDirection(targetPosition, startPosition).Value;
        }

        /// <summary>
        ///     Finds the <see cref="WalkDirection"/> when getting from a start point to a target point.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="startPosition">The start position.</param>
        /// <returns>The <see cref="WalkDirection"/> when going from start to target.</returns>
        public static WalkDirection GetFacingWalkDirection(Vector2 targetPosition, Vector2 startPosition)
        {
            double angle = Math.Atan2(targetPosition.Y - startPosition.Y, targetPosition.X - startPosition.X);

            if (angle <= -Math.PI / 4.0 && angle >= -3.0 * Math.PI / 4.0)
            {
                return WalkDirection.Up;
            }

            if (angle >= -Math.PI / 4.0 && angle <= Math.PI / 4.0)
            {
                return WalkDirection.Right;
            }

            if (angle >= Math.PI / 4.0 && angle <= 3.0 * Math.PI / 4.0)
            {
                return WalkDirection.Down;
            }

            return WalkDirection.Left;
        }

        /// <summary>
        ///     Finds the <see cref="WalkDirection"/> when getting from a start point to a target point.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="startPosition">The start position.</param>
        /// <returns>The <see cref="WalkDirection"/> when going from start to target.</returns>
        public static WalkDirection GetFacingWalkDirection(Point targetPosition, Point startPosition)
        {
            double angle = Math.Atan2(targetPosition.Y - startPosition.Y, targetPosition.X - startPosition.X);

            if (angle <= -Math.PI / 4.0 && angle >= -3.0 * Math.PI / 4.0)
            {
                return WalkDirection.Up;
            }

            if (angle >= -Math.PI / 4.0 && angle <= Math.PI / 4.0)
            {
                return WalkDirection.Right;
            }

            if (angle >= Math.PI / 4.0 && angle <= 3.0 * Math.PI / 4.0)
            {
                return WalkDirection.Down;
            }

            return WalkDirection.Left;
        }

        /// <summary>
        ///     Returns the <see cref="WalkDirection"/> going from a start point to an end point.
        /// </summary>
        /// <param name="start">The start point.</param>
        /// <param name="end">The end point.</param>
        /// <param name="threshold">
        ///     The threshold above which we consider two coordinates to be different.
        /// </param>
        /// <returns>
        ///     The <see cref="WalkDirection"/> going from the start point to the end point.
        /// </returns>
        public static WalkDirection GetWalkDirection(Vector2 start, Vector2 end, float threshold = 0)
        {
            float deltaX = Math.Abs(start.X - end.X);
            float deltaY = Math.Abs(start.Y - end.Y);

            if (deltaX >= threshold && deltaY >= threshold)
            {
                if (start.Y > end.Y)
                {
                    if (start.X < end.X)
                    {
                        return WalkDirection.UpRight;
                    }

                    if (start.X > end.X)
                    {
                        return WalkDirection.UpLeft;
                    }
                }

                if (start.Y < end.Y)
                {
                    if (start.X > end.X)
                    {
                        return WalkDirection.DownLeft;
                    }

                    if (start.X < end.X)
                    {
                        return WalkDirection.DownRight;
                    }
                }
            }

            if (deltaY > deltaX)
            {
                if (start.Y > end.Y)
                {
                    return WalkDirection.Up;
                }

                if (start.Y < end.Y)
                {
                    return WalkDirection.Down;
                }
            }

            if (start.X > end.X)
            {
                return WalkDirection.Left;
            }

            if (start.X < end.X)
            {
                return WalkDirection.Right;
            }

            return WalkDirection.None;
        }

        public static WalkDirection GetWalkDirectionForAngle(float angleDegrees)
        {
            if (angleDegrees < -112.5 && angleDegrees >= -157.5)
            {
                return WalkDirection.UpLeft;
            }

            if (angleDegrees < -67.5 && angleDegrees >= -112.5)
            {
                return WalkDirection.Up;
            }

            if (angleDegrees < -22.5 && angleDegrees >= -67.5)
            {
                return WalkDirection.UpRight;
            }

            if (angleDegrees >= -22.5 && angleDegrees < 22.5)
            {
                return WalkDirection.Right;
            }

            if (angleDegrees >= 22.5 && angleDegrees < 67.5)
            {
                return WalkDirection.DownRight;
            }

            if (angleDegrees >= 67.5 && angleDegrees < 112.5)
            {
                return WalkDirection.Down;
            }

            if (angleDegrees >= 112.5 && angleDegrees < 157.5)
            {
                return WalkDirection.DownLeft;
            }

            return WalkDirection.Left;
        }

        /// <summary>
        ///     Returns the opposite <see cref="WalkDirection"/> from the given direction.
        /// </summary>
        /// <remarks><see cref="WalkDirection.None"/> is opposite to itself.</remarks>
        /// <param name="walkDirection">The direction to find the opposite of.</param>
        /// <returns>The opposite direction from the given direction.</returns>
        public static WalkDirection OppositeWalkDirection(WalkDirection walkDirection)
        {
            if (walkDirection == WalkDirection.Up)
            {
                return WalkDirection.Down;
            }

            if (walkDirection == WalkDirection.Down)
            {
                return WalkDirection.Up;
            }

            if (walkDirection == WalkDirection.Left)
            {
                return WalkDirection.Right;
            }

            if (walkDirection == WalkDirection.Right)
            {
                return WalkDirection.Left;
            }

            if (walkDirection == WalkDirection.UpLeft)
            {
                return WalkDirection.DownRight;
            }

            if (walkDirection == WalkDirection.UpRight)
            {
                return WalkDirection.DownLeft;
            }

            if (walkDirection == WalkDirection.DownLeft)
            {
                return WalkDirection.UpRight;
            }

            if (walkDirection == WalkDirection.DownRight)
            {
                return WalkDirection.UpLeft;
            }

            return WalkDirection.None;
        }
    }
}
