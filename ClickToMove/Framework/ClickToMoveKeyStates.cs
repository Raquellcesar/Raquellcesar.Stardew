// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMoveKeyStates.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Raquellcesar.Stardew.Common;

    public class ClickToMoveKeyStates
    {
        public bool ActionButtonPressed;

        public WalkDirection LastWalkDirection;

        public bool MoveDownHeld;

        public bool MoveDownPressed;

        public bool MoveDownReleased;

        public bool MoveLeftHeld;

        public bool MoveLeftPressed;

        public bool MoveLeftReleased;

        public bool MoveRightHeld;

        public bool MoveRightPressed;

        public bool MoveRightReleased;

        public bool MoveUpHeld;

        public bool MoveUpPressed;

        public bool MoveUpReleased;

        public bool RealClickHeld;

        public bool UseToolButtonHeld;

        public bool UseToolButtonPressed;

        public bool UseToolButtonReleased;

        public void Reset()
        {
            this.StopMoving();
            this.ActionButtonPressed = false;
            this.UseToolButtonPressed = false;
            this.UseToolButtonReleased = true;
            this.UseToolButtonHeld = false;
            this.RealClickHeld = false;
        }

        public void ResetLeftOrRightClickButtons()
        {
            this.ActionButtonPressed = false;
            this.UseToolButtonPressed = false;
            this.UseToolButtonReleased = false;
            this.UseToolButtonHeld = false;
            this.RealClickHeld = false;
        }

        public void SetDown(bool down)
        {
            this.MoveDownPressed = down && !this.MoveDownHeld;
            this.MoveDownReleased = !down && this.MoveDownHeld;
            this.MoveDownHeld = down;
        }

        public void SetLeft(bool left)
        {
            this.MoveLeftPressed = left && !this.MoveLeftHeld;
            this.MoveLeftReleased = !left && this.MoveLeftHeld;
            this.MoveLeftHeld = left;
        }

        public void SetMovePressed(WalkDirection walkDirection)
        {
            this.SetPressed(walkDirection.Y < 0, walkDirection.Y > 0, walkDirection.X < 0, walkDirection.X > 0);

            this.LastWalkDirection = walkDirection;
        }

        public void SetPressed(bool up, bool down, bool left, bool right)
        {
            this.SetUp(up);
            this.SetDown(down);
            this.SetLeft(left);
            this.SetRight(right);
        }

        public void SetRight(bool right)
        {
            this.MoveRightPressed = right && !this.MoveRightHeld;
            this.MoveRightReleased = !right && this.MoveRightHeld;
            this.MoveRightHeld = right;
        }

        public void SetUp(bool up)
        {
            this.MoveUpPressed = up && !this.MoveUpHeld;
            this.MoveUpReleased = !up && this.MoveUpHeld;
            this.MoveUpHeld = up;
        }

        public void SetUseTool(bool useTool)
        {
            this.UseToolButtonPressed = useTool && !this.UseToolButtonHeld;
            this.UseToolButtonReleased = !useTool && this.UseToolButtonHeld;
            this.UseToolButtonHeld = useTool;
        }

        public void StopMoving()
        {
            this.MoveUpReleased = this.MoveUpHeld;
            this.MoveRightReleased = this.MoveRightHeld;
            this.MoveDownReleased = this.MoveDownHeld;
            this.MoveLeftReleased = this.MoveLeftHeld;
            this.MoveUpHeld = false;
            this.MoveRightHeld = false;
            this.MoveDownHeld = false;
            this.MoveLeftHeld = false;
            this.MoveUpPressed = false;
            this.MoveRightPressed = false;
            this.MoveDownPressed = false;
            this.MoveLeftPressed = false;
        }

        public void UpdateReleasedStates()
        {
            this.MoveUpReleased = false;
            this.MoveRightReleased = false;
            this.MoveDownReleased = false;
            this.MoveLeftReleased = false;
            this.UseToolButtonReleased = false;
        }
    }
}