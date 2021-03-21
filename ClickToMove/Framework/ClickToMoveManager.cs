// -----------------------------------------------------------------------
// <copyright file="ClickToMoveManager.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Runtime.CompilerServices;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewModdingAPI;

    using StardewValley;

    /// <summary>
    ///     The manager of all the <see cref="ClickToMove"/> objects created by the mod.
    /// </summary>
    public class ClickToMoveManager
    {
        /// <summary>
        ///     Where all the <see cref="ClickToMove"/> objects created are kept. There's a <see
        ///     cref="ClickToMove"/> object per <see cref="GameLocation"/> and this structure
        ///     frees us from having to track the creation and destruction of each of them. Each
        ///     <see cref="ClickToMove"/> is created on demand and destroyed once there are
        ///     no reference to its <see cref="GameLocation"/> outside the table.
        /// </summary>
        private static readonly ConditionalWeakTable<GameLocation, ClickToMove> GameLocations =
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

        /// <summary>Gets the mod configuration.</summary>
        public static ModConfig Config { get; private set; }

        /// <summary>Gets the helper for writing mods.</summary>
        public static IModHelper Helper { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether an active menu was closed in this tick.
        ///     Used to prevent processing of the left mouse click in this tick
        ///     and also the release of the left mouse button on the next tick.
        /// </summary>
        public static bool JustClosedActiveMenu { get; set; }

        /// <summary>Gets the monitor for monitoring and logging.</summary>
        public static IMonitor Monitor { get; private set; }

        /// <summary>
        ///     Gets or sets a value indicating whether an onscreen menu was clicked in this tick.
        ///     Used to prevent processing of the left mouse click in this tick
        ///     and also the release of the left mouse button on the next tick.
        /// </summary>
        public static bool OnScreenButtonClicked { get; set; }

        /// <summary>Gets the reflection helper, which simplifies access to private game code.</summary>
        public static IReflectionHelper Reflection { get; private set; }

        /// <summary>
        ///     Adds the <see cref="GameLocation" /> and associated <see cref="ClickToMove" /> object
        ///     if the game location doesn't exist, or updates the associated object if it does exist.
        /// </summary>
        /// <param name="location"><see cref="GameLocation" /> to add or update. If it's null, nothing is done.</param>
        /// <param name="clickToMove"><see cref="ClickToMove" /> object to associate with the game location.</param>
        public static void AddOrUpdate(GameLocation location, ClickToMove clickToMove)
        {
            if (location is not null)
            {
                // If we found the key we should just update, if no we should create a new entry.
                if (ClickToMoveManager.GameLocations.TryGetValue(location, out ClickToMove _))
                {
                    ClickToMoveManager.GameLocations.Remove(location);
                    ClickToMoveManager.GameLocations.Add(location, clickToMove);
                }
                else
                {
                    ClickToMoveManager.GameLocations.Add(location, clickToMove);
                }
            }
        }

        /// <summary>Draw the current click to move target.</summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        public static void DrawClickToMoveTarget(SpriteBatch spriteBatch)
        {
            ClickToMove clickToMove = ClickToMoveManager.GetOrCreate(Game1.currentLocation);

            if (clickToMove.TargetNpc is not null)
            {
                Vector2 offset = new Vector2(
                    (clickToMove.TargetNpc.Sprite.SpriteWidth * 4 / 2) - 32,
                    clickToMove.TargetNpc.GetBoundingBox().Height + (clickToMove.TargetNpc.IsMonster ? 0 : 12) - 32);

                spriteBatch.Draw(
                    Game1.mouseCursors,
                    Game1.GlobalToLocal(
                        Game1.viewport,
                        clickToMove.TargetNpc.Position + offset),
                    new Rectangle(194, 388, 16, 16),
                    Color.White,
                    0,
                    Vector2.Zero,
                    4,
                    SpriteEffects.None,
                    0.58f);

                return;
            }

            // Draw click to move target.
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
                            0f,
                            Vector2.Zero,
                            4f,
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
        ///     The <see cref="GameLocation"/> for which to get the <see cref="PathFindingController"/>.
        /// </param>
        /// <returns>
        ///     The <see cref="ClickToMove"/> associated to the given <see cref="GameLocation"/>.
        ///     Returns null if the game location is null.
        /// </returns>
        public static ClickToMove GetOrCreate(GameLocation gameLocation)
        {
            return gameLocation is not null
                       ? ClickToMoveManager.GameLocations.GetValue(gameLocation, ClickToMoveManager.CreateClickToMove)
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
        ///     Creates a new <see cref="ClickToMove"/> object. This method is to be used as a <see
        ///     cref="ConditionalWeakTable{GameLocation, ClickToMove}.CreateValueCallback"/>
        ///     delegate in the <see cref="GetOrCreate"/> method.
        /// </summary>
        /// <param name="gameLocation">
        ///     The <see cref="GameLocation"/> for which to create the <see cref="PathFindingController"/> object.
        /// </param>
        /// <returns>
        ///     A new <see cref="ClickToMove"/> object associated to the given <see cref="GameLocation"/>.
        /// </returns>
        private static ClickToMove CreateClickToMove(GameLocation gameLocation)
        {
            return new ClickToMove(gameLocation);
        }
    }
}
