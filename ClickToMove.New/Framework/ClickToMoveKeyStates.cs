// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMoveKeyStates.cs">
//   Copyright (c) 2021 Raquellcesar
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Raquellcesar.Stardew.Common;

    /// <summary>
    ///     This class keeps information about the state of relevant keys relative to the previous game tick.
    ///     These states don't necessarily result from real physical interactions, they may instead simulate inputs
    ///     that will be used to produce desired outcomes.
    /// </summary>
    public class ClickToMoveKeyStates
    {
        /// <summary>
        ///     Gets or sets the <see cref="WalkDirection"/> of the last movement.
        /// </summary>
        public WalkDirection LastWalkDirection { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move down key is being held.
        /// </summary>
        public bool MoveDownHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move down key was just pressed.
        /// </summary>
        public bool MoveDownPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move down key was just released.
        /// </summary>
        public bool MoveDownReleased { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move left key is being held.
        /// </summary>
        public bool MoveLeftHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move left key was just pressed.
        /// </summary>
        public bool MoveLeftPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move left key was just released.
        /// </summary>
        public bool MoveLeftReleased { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move right key is being held.
        /// </summary>
        public bool MoveRightHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move right key was just pressed.
        /// </summary>
        public bool MoveRightPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move right key was just released.
        /// </summary>
        public bool MoveRightReleased { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move up key is being held.
        /// </summary>
        public bool MoveUpHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move up key was just pressed.
        /// </summary>
        public bool MoveUpPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the move up key was just released.
        /// </summary>
        public bool MoveUpReleased { get; set; }

        public bool RealClickHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the action button was just pressed.
        /// </summary>
        public bool ActionButtonPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the use tool button is being held.
        /// </summary>
        public bool UseToolButtonHeld { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the use tool button was just pressed.
        /// </summary>
        public bool UseToolButtonPressed { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the use tool button was just released.
        /// </summary>
        public bool UseToolButtonReleased { get; set; }

        /// <summary>
        ///  Releases all keys.
        /// </summary>
        public void Reset()
        {
            this.StopMoving();
            this.ActionButtonPressed = false;
            this.UseToolButtonPressed = false;
            this.UseToolButtonReleased = true;
            this.UseToolButtonHeld = false;
            this.RealClickHeld = false;
        }

        /// <summary>
        /// Sets all the click buttons (left and right) states to false.
        /// </summary>
        public void ClearClickButtons()
        {
            this.ActionButtonPressed = false;
            this.UseToolButtonPressed = false;
            this.UseToolButtonReleased = false;
            this.UseToolButtonHeld = false;
            this.RealClickHeld = false;
        }

        /// <summary>
        /// Sets the state of the move down key.
        /// </summary>
        /// <param name="down">Whether the key is down.</param>
        public void SetMoveDown(bool down)
        {
            this.MoveDownPressed = down && !this.MoveDownHeld;
            this.MoveDownReleased = !down && this.MoveDownHeld;
            this.MoveDownHeld = down;
        }

        /// <summary>
        /// Sets the state of the move left key.
        /// </summary>
        /// <param name="left">Whether the key is down.</param>
        public void SetMoveLeft(bool left)
        {
            this.MoveLeftPressed = left && !this.MoveLeftHeld;
            this.MoveLeftReleased = !left && this.MoveLeftHeld;
            this.MoveLeftHeld = left;
        }

        /// <summary>
        /// Sets the state of the movement keys according to a given <see cref="WalkDirection"/>.
        /// </summary>
        /// <param name="walkDirection">The walk direction to follow.</param>
        public void SetMovePressed(WalkDirection walkDirection)
        {
            this.SetPressed(walkDirection.Y < 0, walkDirection.Y > 0, walkDirection.X < 0, walkDirection.X > 0);

            this.LastWalkDirection = walkDirection;
        }

        /// <summary>
        /// Sets the state of the movement keys.
        /// </summary>
        /// <param name="up">Whether the up key is down.</param>
        /// <param name="down">Whether the down key is down.</param>
        /// <param name="left">Whether the left key is down.</param>
        /// <param name="right">Whether the right key is down.</param>
        public void SetPressed(bool up, bool down, bool left, bool right)
        {
            this.SetMoveUp(up);
            this.SetMoveDown(down);
            this.SetMoveLeft(left);
            this.SetMoveRight(right);
        }

        /// <summary>
        /// Sets the state of the move right key.
        /// </summary>
        /// <param name="right">Whether the key is down.</param>
        public void SetMoveRight(bool right)
        {
            this.MoveRightPressed = right && !this.MoveRightHeld;
            this.MoveRightReleased = !right && this.MoveRightHeld;
            this.MoveRightHeld = right;
        }

        /// <summary>
        /// Sets the state of the move up key.
        /// </summary>
        /// <param name="up">Whether the key is down.</param>
        public void SetMoveUp(bool up)
        {
            this.MoveUpPressed = up && !this.MoveUpHeld;
            this.MoveUpReleased = !up && this.MoveUpHeld;
            this.MoveUpHeld = up;
        }

        /// <summary>
        ///  Sets the current state of the UseToolButton.
        /// </summary>
        /// <param name="useTool">Whether the button is down during this tick.</param>
        public void SetUseTool(bool useTool)
        {
            this.UseToolButtonPressed = useTool && !this.UseToolButtonHeld;
            this.UseToolButtonReleased = !useTool && this.UseToolButtonHeld;
            this.UseToolButtonHeld = useTool;
        }

        /// <summary>
        ///     Releases all movement keys.
        /// </summary>
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

        /// <summary>
        ///     Sets all released states to false.
        /// </summary>
        public void ClearReleasedStates()
        {
            this.MoveUpReleased = false;
            this.MoveRightReleased = false;
            this.MoveDownReleased = false;
            this.MoveLeftReleased = false;
            this.UseToolButtonReleased = false;
        }
    }
}
