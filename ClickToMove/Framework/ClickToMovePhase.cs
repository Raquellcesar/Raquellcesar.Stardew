// -----------------------------------------------------------------------
// <copyright file="ClickToMovePhase.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    /// <summary>
    ///     The phase of the <see cref="ClickToMove"/> for the current tick. Each phase determines
    ///     the actions to be performed during the controller's update.
    /// </summary>
    internal enum ClickToMovePhase
    {
        /// <summary>
        ///     Nothing is happening.
        /// </summary>
        None,

        /// <summary>
        ///     The farmer is following a computed path.
        /// </summary>
        FollowingPath,

        /// <summary>
        ///     The farmer is on the final tile of the path.
        /// </summary>
        OnFinalTile,

        ReachedEndOfPath,

        Complete,

        /// <summary>
        ///     The farmer will use a tool on this tick.
        /// </summary>
        UseTool,

        /// <summary>
        ///     The farmer will release the current tool on this tick.
        /// </summary>
        ReleaseTool,

        CheckForMoreClicks,

        PendingComplete,

        ClickHeld,

        /// <summary>
        ///     The farmer will perform an action on this tick.
        /// </summary>
        DoAction,

        FinishAction,

        UsingJoyStick,

        AttackInNewDirection,
    }
}
