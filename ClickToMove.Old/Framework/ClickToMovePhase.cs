// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMovePhase.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    internal enum ClickToMovePhase
    {
        None,

        FollowingPath,

        OnFinalTile,

        ReachedEndOfPath,

        Complete,

        UseTool,

        ReleaseTool,

        CheckForMoreClicks,

        PendingComplete,

        ClickHeld,

        DoAction,

        FinishAction,

        UsingJoyStick,

        AttackInNewDirection
    }
}