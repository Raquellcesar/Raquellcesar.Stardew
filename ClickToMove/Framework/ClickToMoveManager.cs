// -----------------------------------------------------------------------
// <copyright file="ClickToMoveManager.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

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
    ///     The manager of all the <see cref="ClickToMove"/> objects created by the mod.
    /// </summary>
    public static class ClickToMoveManager
    {
        /// <summary>
        ///     Where all the <see cref="ClickToMove"/> objects created are kept. There's a <see
        ///     cref="ClickToMove"/> object per <see cref="GameLocation"/> and this structure frees
        ///     us from having to track the creation and destruction of each of them. Each <see
        ///     cref="ClickToMove"/> is created on demand and destroyed once there are no reference
        ///     to its <see cref="GameLocation"/> outside the table.
        /// </summary>
        private static readonly ConditionalWeakTable<GameLocation, ClickToMove> PathFindingControllers =
            new ConditionalWeakTable<GameLocation, ClickToMove>();

        /// <summary>
        ///     The current frame for the click to move target animation.
        /// </summary>
        private static int greenSquareAnimIndex;

        /// <summary>
        ///     The last time the click to move target frame animation was updated.
        /// </summary>
        private static long greenSquareLastUpdateTicks = DateTime.Now.Ticks;

        /// <summary>
        ///     The texture to use for signaling path destinations.
        /// </summary>
        private static Texture2D targetTexture;

        /// <summary>
        ///     Gets the mod configuration.
        /// </summary>
        public static ModConfig Config { get; private set; }

        /// <summary>
        ///     Gets the helper for writing mods.
        /// </summary>
        public static IModHelper Helper { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether an active menu was closed in this tick. Used
        ///     to prevent processing of the left mouse click in this tick and also the release of
        ///     the left mouse button on the next tick.
        /// </summary>
        public static bool JustClosedActiveMenu { get; set; }

        /// <summary>
        ///     Gets the monitor for monitoring and logging.
        /// </summary>
        public static IMonitor Monitor { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether an onscreen menu was clicked in this tick.
        ///     Used to prevent processing of the left mouse click in this tick and also the release
        ///     of the left mouse button on the next tick.
        /// </summary>
        public static bool OnScreenButtonClicked { get; set; }

        /// <summary>
        ///     Gets the reflection helper, which simplifies access to private game code.
        /// </summary>
        public static IReflectionHelper Reflection { get; private set; }

        /// <summary>
        ///     Adds the <see cref="GameLocation"/> and associated <see cref="ClickToMove"/> object
        ///     if the game location doesn't exist, or updates the associated object if it does exist.
        /// </summary>
        /// <param name="location">
        ///     <see cref="GameLocation"/> to add or update. If it's null, nothing is done.
        /// </param>
        /// <param name="clickToMove">
        ///     <see cref="ClickToMove"/> object to associate with the game location.
        /// </param>
        public static void AddOrUpdate(GameLocation location, ClickToMove clickToMove)
        {
            if (location is not null)
            {
                // If we found the key we should just update, if no we should create a new entry.
                if (ClickToMoveManager.PathFindingControllers.TryGetValue(location, out ClickToMove _))
                {
                    ClickToMoveManager.PathFindingControllers.Remove(location);
                    ClickToMoveManager.PathFindingControllers.Add(location, clickToMove);
                }
                else
                {
                    ClickToMoveManager.PathFindingControllers.Add(location, clickToMove);
                }
            }
        }

        /// <summary>
        ///     Draw the current click to move target.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        public static void DrawClickToMoveTarget(SpriteBatch spriteBatch)
        {
            ClickToMove clickToMove = ClickToMoveManager.GetOrCreate(Game1.currentLocation);

            if (clickToMove.TargetNpc is null && (Game1.displayHUD || Game1.eventUp) && Game1.currentBillboard == 0
                && Game1.gameMode == Game1.playingGameMode && !Game1.freezeControls && !Game1.panMode
                && !Game1.HostPaused)
            {
                if (Game1.activeClickableMenu is not null && clickToMove.ClickedTile.X != -1)
                {
                    clickToMove.Reset();
                }
                else if (Game1.player.canMove)
                {
                    if (clickToMove.ClickedTile.X != -1 && clickToMove.TargetFarmAnimal is null)
                    {
                        Vector2 vector = new Vector2(
                            (clickToMove.ClickedTile.X * Game1.tileSize) - Game1.viewport.X,
                            (clickToMove.ClickedTile.Y * Game1.tileSize) - Game1.viewport.Y);

                        long ticks = DateTime.Now.Ticks;

                        // Update frame every 125 ms.
                        if (ticks - ClickToMoveManager.greenSquareLastUpdateTicks > 1250000)
                        {
                            ClickToMoveManager.greenSquareLastUpdateTicks = ticks;

                            ClickToMoveManager.greenSquareAnimIndex++;
                            if (ClickToMoveManager.greenSquareAnimIndex > 7)
                            {
                                ClickToMoveManager.greenSquareAnimIndex = 0;
                            }
                        }

                        spriteBatch.Draw(
                            ClickToMoveManager.targetTexture,
                            vector + new Vector2(0, -12),
                            new Rectangle(ClickToMoveManager.greenSquareAnimIndex * 16, 0, 16, 20),
                            Color.White * Game1.mouseCursorTransparency,
                            0,
                            Vector2.Zero,
                            4,
                            SpriteEffects.None,
                            0.58f);
                    }
                    else if (clickToMove.NoPathHere.X != -1)
                    {
                        Vector2 position = new Vector2(
                            (clickToMove.NoPathHere.X * Game1.tileSize) - Game1.viewport.X,
                            (clickToMove.NoPathHere.Y * Game1.tileSize) - Game1.viewport.Y);

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
        }

        /// <summary>
        ///     Gets the <see cref="ClickToMove"/> object associated to a given <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation"/> for which to get the <see cref="ClickToMove"/> object.
        /// </param>
        /// <returns>
        ///     The <see cref="ClickToMove"/> associated to the given <see cref="GameLocation"/>.
        ///     Returns <see langword="null"/> if the game location is null.
        /// </returns>
        public static ClickToMove GetOrCreate(GameLocation gameLocation)
        {
            return gameLocation is not null
                       ? ClickToMoveManager.PathFindingControllers.GetValue(gameLocation, ClickToMoveManager.CreateClickToMove)
                       : null;
        }

        /// <summary>
        ///     Initializes the class helpers.
        /// </summary>
        /// <param name="config">The mod configuration.</param>
        /// <param name="monitor">The monitor for monitoring and logging.</param>
        /// <param name="helper">The helper for writing mods.</param>
        public static void Init(ModConfig config, IMonitor monitor, IModHelper helper)
        {
            ClickToMoveManager.Config = config;
            ClickToMoveManager.Monitor = monitor;
            ClickToMoveManager.Helper = helper;
            ClickToMoveManager.Reflection = helper.Reflection;

            ClickToMoveManager.targetTexture =
                ClickToMoveManager.Helper.Content.Load<Texture2D>("assets/clickTarget.png");
        }

        /// <summary>
        ///     Method called when the simulated left click is pressed in this <see cref="FishingGame"/>.
        /// </summary>
        /// <param name="fishingGame">The <see cref="FishingGame"/> instance.</param>
        /// <param name="timerToStart">The time until the minigame starts.</param>
        public static void OnLeftClick(this FishingGame fishingGame, int timerToStart)
        {
            if (!Game1.isAnyGamePadButtonBeingPressed())
            {
                int showResultsTimer = MinigamesPatcher.FishingGameShowResultsTimer;

                FishingRod fishingRod = Game1.player.CurrentTool as FishingRod;

                if (timerToStart <= 0
                    && showResultsTimer < 0
                    && !fishingGame.gameDone
                    && Game1.activeClickableMenu is null
                    && !fishingRod.hit
                    && !fishingRod.pullingOutOfWater
                    && !fishingRod.isCasting
                    && !fishingRod.fishCaught
                    && !fishingRod.castedButBobberStillInAir)
                {
                    Game1.player.lastClick = Vector2.Zero;
                    Game1.player.Halt();
                    Game1.pressUseToolButton();

                    return;
                }

                switch (showResultsTimer)
                {
                    case > 11000:
                        MinigamesPatcher.FishingGameShowResultsTimer = 11001;
                        break;
                    case > 9000:
                        MinigamesPatcher.FishingGameShowResultsTimer = 9001;
                        break;
                    case > 7000:
                        MinigamesPatcher.FishingGameShowResultsTimer = 7001;
                        break;
                    case > 5000:
                        MinigamesPatcher.FishingGameShowResultsTimer = 5001;
                        break;
                    case > 1000 and < 5000:
                        MinigamesPatcher.FishingGameShowResultsTimer = 1500;
                        Game1.playSound("smallSelect");
                        break;
                }
            }
        }

        /// <summary>
        ///     Deals with the simulated inputs while playing the <see cref="FishingGame"/>.
        /// </summary>
        /// <param name="fishingGame">The <see cref="FishingGame"/> instance.</param>
        /// <param name="clickToMove">
        ///     The <see cref="ClickToMove"/> object associated to the minigame's location.
        /// </param>
        public static void ReceiveClickToMoveKeyStates(this FishingGame fishingGame, ClickToMove clickToMove)
        {
            ClickToMoveKeyStates clickKeyStates = clickToMove.ClickKeyStates;

            if (clickKeyStates.MoveUpReleased)
            {
                Game1.player.setMoving(Farmer.release + Farmer.up);
            }

            if (clickKeyStates.MoveRightReleased)
            {
                Game1.player.setMoving(Farmer.release + Farmer.right);
            }

            if (clickKeyStates.MoveDownReleased)
            {
                Game1.player.setMoving(Farmer.release + Farmer.down);
            }

            if (clickKeyStates.MoveLeftReleased)
            {
                Game1.player.setMoving(Farmer.release + Farmer.left);
            }

            int timerToStart = MinigamesPatcher.FishingGameTimerToStart;

            if (!fishingGame.gameDone && !Game1.player.UsingTool && timerToStart <= 0)
            {
                if ((clickKeyStates.MoveUpPressed && !clickKeyStates.MoveUpReleased) || clickKeyStates.MoveUpHeld)
                {
                    Game1.player.setMoving(Farmer.up);
                }
                else if ((clickKeyStates.MoveRightPressed && !clickKeyStates.MoveRightReleased)
                         || clickKeyStates.MoveRightHeld)
                {
                    Game1.player.setMoving(Farmer.right);
                }
                else if ((clickKeyStates.MoveDownPressed && !clickKeyStates.MoveDownReleased)
                         || clickKeyStates.MoveDownHeld)
                {
                    Game1.player.setMoving(Farmer.down);
                }

                if ((clickKeyStates.MoveLeftPressed && !clickKeyStates.MoveLeftReleased) || clickKeyStates.MoveLeftHeld)
                {
                    Game1.player.setMoving(Farmer.left);
                }
            }

            if (clickKeyStates.UseToolButtonPressed)
            {
                fishingGame.OnLeftClick(timerToStart);
            }

            if (clickKeyStates.UseToolButtonReleased)
            {
                ClickToMoveManager.OnLeftClickRelease(clickToMove.GameLocation, clickToMove.ClickPoint.X, clickToMove.ClickPoint.Y);
            }
        }

        /// <summary>
        ///     Updates the state of the current minigame.
        /// </summary>
        /// <param name="currentMouseState">The current <see cref="MouseState"/>.</param>
        public static void UpdateMinigameInput(MouseState currentMouseState)
        {
            GameLocation location = Game1.currentLocation;

            if (location is not null)
            {
                if (Game1.currentMinigame is FishingGame fishingGame && fishingGame.gameDone)
                {
                    return;
                }

                ClickToMove clickToMove = ClickToMoveManager.GetOrCreate(location);

                if (currentMouseState.LeftButton == ButtonState.Pressed
                    && Game1.oldMouseState.LeftButton == ButtonState.Released)
                {
                    clickToMove.OnClick(
                        Game1.getMouseX() + Game1.viewport.X,
                        Game1.getMouseY() + Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Pressed
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    clickToMove.OnClickHeld(
                        Game1.getMouseX() + Game1.viewport.X,
                        Game1.getMouseY() + Game1.viewport.Y);
                }
                else if (currentMouseState.LeftButton == ButtonState.Released
                         && Game1.oldMouseState.LeftButton == ButtonState.Pressed)
                {
                    clickToMove.OnClickRelease(
                        Game1.getMouseX() + Game1.viewport.X,
                        Game1.getMouseY() + Game1.viewport.Y);
                }

                clickToMove.Update();

                if (Game1.currentMinigame is FishingGame fishingGame2)
                {
                    fishingGame2.ReceiveClickToMoveKeyStates(clickToMove);
                }
            }
        }

        /// <summary>
        ///     Creates a new <see cref="ClickToMove"/> object. This method is to be used as a <see
        ///     cref="ConditionalWeakTable{GameLocation, ClickToMove}.CreateValueCallback"/>
        ///     delegate in the <see cref="GetOrCreate"/> method.
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation"/> for which to create the <see cref="ClickToMove"/> object.
        /// </param>
        /// <returns>
        ///     A new <see cref="ClickToMove"/> object associated to the given <see cref="GameLocation"/>.
        /// </returns>
        private static ClickToMove CreateClickToMove(GameLocation gameLocation)
        {
            return new ClickToMove(gameLocation);
        }

        /// <summary>
        ///     Method called when the simulated left click is released.
        /// </summary>
        /// <param name="gameLocation">The current <see cref="GameLocation"/>.</param>
        /// <param name="x">The clicked x coordinate.</param>
        /// <param name="y">The clicked y coordinate.</param>
        private static void OnLeftClickRelease(GameLocation gameLocation, int x, int y)
        {
            if (MinigamesPatcher.FishingGameShowResultsTimer < 0
                && Game1.player.CurrentTool is FishingRod fishingRod
                && !fishingRod.isCasting
                && Game1.activeClickableMenu is null
                && Game1.player.CurrentTool.onRelease(gameLocation, x, y, Game1.player))
            {
                Game1.player.Halt();
            }
        }
    }
}
