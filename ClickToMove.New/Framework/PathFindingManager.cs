// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="PathFindingManager.cs">
//     Copyright (c) 2021 Raquellcesar
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Runtime.CompilerServices;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;
    using Microsoft.Xna.Framework.Input;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Minigames;
    using StardewValley.Tools;

    /// <summary>
    ///     The manager of all the <see cref="PathFindingController"/> objects created by the mod.
    /// </summary>
    internal class PathFindingManager
    {
        /// <summary>
        ///     Minimum 125 ms between animation frames.
        /// </summary>
        private const long MinimumTicksbetweenTargetAnimation = 1250000;

        /// <summary>
        ///     The texture to use for signaling path destinations.
        /// </summary>
        private static Texture2D targetTexture;

        /// <summary>
        ///     Where all the <see cref="PathFindingController"/> created are kept. There's a <see
        ///     cref="PathFindingController"/> per <see cref="GameLocation"/> and this structure
        ///     frees us from having to track the creation and destruction of each of them. Each
        ///     <see cref="PathFindController"/> is created on demand and destroyed once there are
        ///     no reference to its <see cref="GameLocation"/> outside the table.
        /// </summary>
        private readonly ConditionalWeakTable<GameLocation, PathFindingController> gameLocations =
            new ConditionalWeakTable<GameLocation, PathFindingController>();

        /// <summary>
        ///     Provides simplified APIs for writing mods.
        /// </summary>
        private readonly IModHelper helper;

        /// <summary>
        ///     The current frame for the click to move target animation.
        /// </summary>
        private int greenSquareAnimIndex;

        /// <summary>
        ///     The last time the click to move target frame animation was updated.
        /// </summary>
        private long greenSquareLastUpdateTicks = DateTime.Now.Ticks;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PathFindingManager"/> class.
        /// </summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public PathFindingManager(IModHelper helper)
        {
            this.helper = helper;

            PathFindingManager.targetTexture = this.helper.Content.Load<Texture2D>("assets/clickTarget.png");
        }

        /// <summary>
        ///     Gets a managed <see cref="PathFindingController"/> using [] notation.
        /// </summary>
        /// <param name="gameLocation">The game location to which the object is linked.</param>
        /// <returns>
        ///     The <see cref="PathFindingController"/> associated with the given game location.
        /// </returns>
        /// <seealso cref="GetOrCreate"/>
        public PathFindingController this[GameLocation gameLocation]
        {
            get
            {
                return this.GetOrCreate(gameLocation);
            }

            set
            {
                this.AddOrUpdate(gameLocation, value);
            }
        }

        /// <summary>
        ///     Draw the current click to move target.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        public void DrawPathTarget(SpriteBatch spriteBatch)
        {
            PathFindingController pathFindingController = this.GetOrCreate(Game1.currentLocation);

            NPC npc = pathFindingController.TargetNpc;

            if (npc is not null)
            {
                Game1.spriteBatch.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport,
                        npc.Position + new Vector2((npc.Sprite.SpriteWidth * 4 / 2) - (Game1.tileSize / 2), npc.GetBoundingBox().Height + ((!npc.IsMonster) ? 12 : 0) - (Game1.tileSize / 2))),
                    new Microsoft.Xna.Framework.Rectangle(194, 388, 16, 16),
                    Color.White,
                    0,
                    Vector2.Zero,
                    4,
                    SpriteEffects.None,
                    0.58f);
            }
            else if ((Game1.displayHUD || Game1.eventUp) && Game1.currentBillboard == 0
                && Game1.gameMode == Game1.playingGameMode && !Game1.freezeControls && !Game1.panMode
                && !Game1.HostPaused)
            {
                if (Game1.activeClickableMenu is not null && pathFindingController.ClickedTile.X != -1)
                {
                    pathFindingController.Reset();
                }
                else if (Game1.player.canMove && pathFindingController.ClickedTile.X != -1 && pathFindingController.TargetNpc is null
                         && pathFindingController.TargetFarmAnimal is null)
                {
                    Vector2 vector = new Vector2(
                        (pathFindingController.ClickedTile.X * Game1.tileSize) - Game1.viewport.X,
                        (pathFindingController.ClickedTile.Y * Game1.tileSize) - Game1.viewport.Y);

                    long ticks = DateTime.Now.Ticks;

                    // Update frame every 125 ms.
                    if (ticks - this.greenSquareLastUpdateTicks > 1250000)
                    {
                        this.greenSquareLastUpdateTicks = ticks;

                        this.greenSquareAnimIndex++;
                        if (this.greenSquareAnimIndex > 7)
                        {
                            this.greenSquareAnimIndex = 0;
                        }
                    }

                    spriteBatch.Draw(
                        PathFindingManager.targetTexture,
                        vector + new Vector2(0f, -12f),
                        new Rectangle(this.greenSquareAnimIndex * 16, 0, 16, 20),
                        Color.White * Game1.mouseCursorTransparency,
                        0,
                        Vector2.Zero,
                        4,
                        SpriteEffects.None,
                        0.58f);
                }
                else if (Game1.player.canMove && pathFindingController.NoPathHere.X != -1)
                {
                    Vector2 position = new Vector2(
                        (pathFindingController.NoPathHere.X * Game1.tileSize) - Game1.viewport.X,
                        (pathFindingController.NoPathHere.Y * Game1.tileSize) - Game1.viewport.Y);

                    spriteBatch.Draw(
                        Game1.mouseCursors,
                        position,
                        new Rectangle(210, 388, 16, 16),
                        Color.White,
                        0,
                        Vector2.Zero,
                        4,
                        SpriteEffects.None,
                        0.01f);
                }
            }
        }

        /// <summary>
        ///     Adds the <see cref="GameLocation" /> and associated <see cref="PathFindingController" /> object
        ///     if the game location doesn't exist, or updates the associated object if it does exist.
        /// </summary>
        /// <param name="gameLocation"><see cref="GameLocation" /> to add or update. If it's null, nothing is done.</param>
        /// <param name="pathFindingController"><see cref="PathFindingController" /> object to associate with the game location.</param>
        public void AddOrUpdate(GameLocation gameLocation, PathFindingController pathFindingController)
        {
            if (gameLocation is not null)
            {
                // If we found the key we should just update, if not we should create a new entry.
                if (this.gameLocations.TryGetValue(gameLocation, out PathFindingController _))
                {
                    this.gameLocations.Remove(gameLocation);
                    this.gameLocations.Add(gameLocation, pathFindingController);

                    GC.Collect(0, GCCollectionMode.Forced);
                }
                else
                {
                    this.gameLocations.Add(gameLocation, pathFindingController);
                }
            }
        }

        /// <summary>
        ///     Gets the <see cref="PathFindingController"/> object associated to a given <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="GameLocation"/> for which to get the <see cref="PathFindingController"/>.
        /// </param>
        /// <returns>
        ///     The <see cref="PathFindingController"/> associated to the given <see cref="GameLocation"/>.
        ///     Returns null if the game location is null.
        /// </returns>
        public PathFindingController GetOrCreate(GameLocation location)
        {
            return location is not null ? this.gameLocations.GetValue(location, this.CreateClickToMove) : null;
        }

        public void UpdateMinigame(MouseState currentMouseState)
        {
            GameLocation gameLocation = Game1.currentLocation;

            if (gameLocation is not null)
            {
                if (Game1.currentMinigame is FishingGame fishingGame)
                {
                    gameLocation = this.helper.Reflection.GetField<GameLocation>(fishingGame, "location").GetValue();
                }
                else if (Game1.currentMinigame is TargetGame targetGame)
                {
                    gameLocation = this.helper.Reflection.GetField<GameLocation>(targetGame, "location").GetValue();
                }

                PathFindingController pathFindingController = this[gameLocation];

                if (currentMouseState.LeftButton == ButtonState.Pressed
                    && Game1.oldMouseState.LeftButton == ButtonState.Released)
                {
                    pathFindingController.OnClick(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Pressed
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    pathFindingController.OnClickHeld(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Released
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    pathFindingController.OnClickRelease(
                        Game1.getMouseX(),
                        Game1.getMouseY(),
                        Game1.viewport.X,
                        Game1.viewport.Y);
                }

                pathFindingController.Update();

                if (Game1.currentMinigame is FishingGame fishingGame2)
                {
                    this.ReceiveClickToMoveKeyStates(fishingGame2, pathFindingController);
                }
                else if (Game1.currentMinigame is TargetGame targetGame)
                {
                    this.ReceiveClickToMoveKeyStates(targetGame, pathFindingController);
                }
            }
        }

        /// <summary>
        ///     Creates a new <see cref="PathFindingController"/> object. This method is to be used as a <see
        ///     cref="ConditionalWeakTable{GameLocation, ClickToMove}.CreateValueCallback"/>
        ///     delegate in the <see cref="GetOrCreate"/> method.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="GameLocation"/> for which to create the <see cref="PathFindingController"/> object.
        /// </param>
        /// <returns>
        ///     A new <see cref="PathFindingController"/> object associated to the given <see cref="GameLocation"/>.
        /// </returns>
        private PathFindingController CreateClickToMove(GameLocation location)
        {
            return new PathFindingController(location, this.helper.Reflection);
        }

        private void OnRightClick(FishingGame fishingGame, int timerToStart)
        {
            if (Game1.isAnyGamePadButtonBeingPressed())
            {
                IReflectedField<int> showResultsTimerField =
                    this.helper.Reflection.GetField<int>(fishingGame, "showResultsTimer");

                int showResultsTimer = showResultsTimerField.GetValue();

                FishingRod fishingRod = Game1.player.CurrentTool as FishingRod;

                if (timerToStart <= 0 && showResultsTimer < 0 && !fishingGame.gameDone
                    && Game1.activeClickableMenu is null && !fishingRod.hit && !fishingRod.pullingOutOfWater
                    && !fishingRod.isCasting && !fishingRod.fishCaught)
                {
                    Game1.player.lastClick = Vector2.Zero;
                    Game1.player.Halt();
                    Game1.pressUseToolButton();
                }
                else if (showResultsTimer > 11000)
                {
                    showResultsTimerField.SetValue(11001);
                }
                else if (showResultsTimer > 9000)
                {
                    showResultsTimerField.SetValue(9001);
                }
                else if (showResultsTimer > 7000)
                {
                    showResultsTimerField.SetValue(7001);
                }
                else if (showResultsTimer > 5000)
                {
                    showResultsTimerField.SetValue(5001);
                }
                else if (showResultsTimer < 5000 && showResultsTimer > 1000)
                {
                    showResultsTimerField.SetValue(1500);
                    Game1.playSound("smallSelect");
                }
            }
        }

        private void ReceiveClickToMoveKeyStates(FishingGame fishingGame, PathFindingController pathFindingController)
        {
            ClickToMoveKeyStates clickKeyStates = pathFindingController.KeyStates;

            if (clickKeyStates.MoveUpReleased)
            {
                Game1.player.setMoving(33);
            }

            if (clickKeyStates.MoveDownReleased)
            {
                Game1.player.setMoving(36);
            }

            if (clickKeyStates.MoveLeftReleased)
            {
                Game1.player.setMoving(40);
            }

            if (clickKeyStates.MoveRightReleased)
            {
                Game1.player.setMoving(34);
            }

            int timerToStart = this.helper.Reflection.GetField<int>(fishingGame, "timerToStart").GetValue();

            if (!fishingGame.gameDone && !Game1.player.UsingTool && timerToStart <= 0)
            {
                if ((clickKeyStates.MoveUpPressed && !clickKeyStates.MoveUpReleased) || clickKeyStates.MoveUpHeld)
                {
                    Game1.player.setMoving(1);
                }
                else if ((clickKeyStates.MoveDownPressed && !clickKeyStates.MoveDownReleased)
                         || clickKeyStates.MoveDownHeld)
                {
                    Game1.player.setMoving(4);
                }

                if ((clickKeyStates.MoveLeftPressed && !clickKeyStates.MoveLeftReleased) || clickKeyStates.MoveLeftHeld)
                {
                    Game1.player.setMoving(8);
                }
                else if ((clickKeyStates.MoveRightPressed && !clickKeyStates.MoveRightReleased)
                         || clickKeyStates.MoveRightHeld)
                {
                    Game1.player.setMoving(2);
                }
            }

            if (ClickToMovePatcher.LeftClickNextUpdateFishingGame)
            {
                fishingGame.receiveLeftClick(0, 0);
                ClickToMovePatcher.LeftClickNextUpdateFishingGame = false;
            }

            if (clickKeyStates.UseToolButtonPressed)
            {
                ClickToMovePatcher.LeftClickNextUpdateFishingGame = true;
            }

            if (clickKeyStates.UseToolButtonReleased)
            {
                fishingGame.releaseLeftClick(
                    (int)pathFindingController.ClickPoint.X,
                    (int)pathFindingController.ClickPoint.Y);
            }

            if (clickKeyStates.ActionButtonPressed)
            {
                this.OnRightClick(fishingGame, timerToStart);
            }
        }

        private void ReceiveClickToMoveKeyStates(TargetGame targetGame, PathFindingController pathFindingController)
        {
            ClickToMoveKeyStates clickKeyStates = pathFindingController.KeyStates;

            if (this.helper.Reflection.GetField<int>(targetGame, "showResultsTimer").GetValue() > 0
                || this.helper.Reflection.GetField<int>(targetGame, "gameEndTimer").GetValue() < 0)
            {
                Game1.player.Halt();
                return;
            }

            if (Game1.player.movementDirections.Count < 2 && !Game1.player.UsingTool
                                                          && this.helper.Reflection.GetField<int>(
                                                              targetGame,
                                                              "timerToStart").GetValue() <= 0)
            {
                if ((clickKeyStates.MoveUpPressed && !clickKeyStates.MoveUpReleased) || clickKeyStates.MoveUpHeld)
                {
                    Game1.player.setMoving(1);
                }
                else if ((clickKeyStates.MoveDownPressed && !clickKeyStates.MoveDownReleased)
                         || clickKeyStates.MoveDownHeld)
                {
                    Game1.player.setMoving(4);
                }

                if ((clickKeyStates.MoveLeftPressed && !clickKeyStates.MoveLeftReleased) || clickKeyStates.MoveLeftHeld)
                {
                    Game1.player.setMoving(8);
                }
                else if ((clickKeyStates.MoveRightPressed && !clickKeyStates.MoveRightReleased)
                         || clickKeyStates.MoveRightHeld)
                {
                    Game1.player.setMoving(2);
                }
            }

            if (clickKeyStates.MoveUpReleased)
            {
                Game1.player.setMoving(33);
            }

            if (clickKeyStates.MoveDownReleased)
            {
                Game1.player.setMoving(36);
            }

            if (clickKeyStates.MoveLeftReleased)
            {
                Game1.player.setMoving(40);
            }

            if (clickKeyStates.MoveRightReleased)
            {
                Game1.player.setMoving(34);
            }
        }
    }
}
