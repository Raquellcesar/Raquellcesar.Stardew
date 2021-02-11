// // --------------------------------------------------------------------------------------------------------------------
// // <copyright company="Raquellcesar" file="ClickToMoveManager.cs">
// //   Copyright (c) 2021 Raquellcesar
// //
// //   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
// //   or at https://opensource.org/licenses/MIT.
// // </copyright>
// // --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Runtime.CompilerServices;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewModdingAPI;

    using StardewValley;

    public class ClickToMoveManager
    {
        public static bool OnScreenButtonClicked { get; set; }

        /// <summary>
        ///     Enables attachment of a <see cref="ClickToMove" /> object to a <see cref="GameLocation" /> at run time.
        /// </summary>
        private static readonly ConditionalWeakTable<GameLocation, ClickToMove> GameLocations =
            new ConditionalWeakTable<GameLocation, ClickToMove>();

        private static int greenSquareAnimIndex;

        private static long greenSquareLastUpdateTicks = DateTime.Now.Ticks;

        private static Texture2D targetTexture;

        /// <summary>The mod configuration.</summary>
        public static ModConfig Config { get; private set; }

        /// <summary>Provides simplified APIs for writing mods.</summary>
        public static IModHelper Helper { get; private set; }

        /// <summary>Encapsulates monitoring and logging.</summary>
        public static IMonitor Monitor { get; private set; }

        /// <summary>Simplifies access to private game code.</summary>
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

            // Draw click to move target.
            if (clickToMove.TargetNpc is null && (Game1.displayHUD || Game1.eventUp) && Game1.currentBillboard == 0
                && Game1.gameMode == Game1.playingGameMode && !Game1.freezeControls && !Game1.panMode
                && !Game1.HostPaused)
            {
                if (Game1.activeClickableMenu is not null && clickToMove.ClickedTile.X != -1)
                {
                    clickToMove.Reset();
                }
                else if (Game1.player.canMove && clickToMove.ClickedTile.X != -1 && clickToMove.TargetNpc is null
                         && clickToMove.TargetFarmAnimal is null)
                {
                    Vector2 vector = new Vector2(
                        clickToMove.ClickedTile.X * Game1.tileSize - Game1.viewport.X,
                        clickToMove.ClickedTile.Y * Game1.tileSize - Game1.viewport.Y);

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
                        vector + new Vector2(0f, -12f),
                        new Rectangle(ClickToMoveManager.greenSquareAnimIndex * 16, 0, 16, 20),
                        Color.White * Game1.mouseCursorTransparency,
                        0f,
                        Vector2.Zero,
                        4f,
                        SpriteEffects.None,
                        0.58f);
                }
                else if (Game1.player.canMove && clickToMove.NoPathHere.X != -1)
                {
                    Vector2 position = new Vector2(
                        clickToMove.NoPathHere.X * Game1.tileSize - Game1.viewport.X,
                        clickToMove.NoPathHere.Y * Game1.tileSize - Game1.viewport.Y);

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

        public static ClickToMove GetOrCreate(GameLocation location)
        {
            return location is not null
                       ? ClickToMoveManager.GameLocations.GetValue(location, ClickToMoveManager.CreateClickToMove)
                       : null;
        }

        public static void Init(ModConfig config, IMonitor monitor, IModHelper helper, IReflectionHelper reflection)
        {
            ClickToMoveManager.Config = config;
            ClickToMoveManager.Helper = helper;
            ClickToMoveManager.Monitor = monitor;
            ClickToMoveManager.Reflection = reflection;

            ClickToMoveManager.targetTexture =
                ClickToMoveManager.Helper.Content.Load<Texture2D>("assets/clickTarget.png");
        }

        private static ClickToMove CreateClickToMove(GameLocation location)
        {
            return new ClickToMove(location);
        }
    }
}
