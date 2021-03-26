// ----------------------------------------------------------------------- ------------------------------------------------------------------------------------------------
// <copyright file="ObjectsPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------ -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Objects;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Object"/> classes.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class ObjectsPatcher
    {
        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            /*harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.drawPlacementBounds)),
                prefix: new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeDrawPlacementBounds)));*/

            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.drawPlacementBounds)),
                transpiler: new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.TranspileDrawPlacementBounds)));

            harmony.Patch(
                AccessTools.Method(typeof(Wallpaper), nameof(Wallpaper.placementAction)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeWallpaperPlacementAction)));
        }

        /// <summary>
        ///     Determines if an <see cref="Item"/> can be placed by a <see cref="Farmer"/> at a
        ///     given position in a <see cref="GameLocation"/>. This method replicates
        ///     Utility.playerCanPlaceItemHere, ignoring restrictions on the distance of the
        ///     placement position from the player. To be used by <see cref="TranspileDrawPlacementBounds"/>.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where to place the item.</param>
        /// <param name="item">The <see cref="Item"/> to place.</param>
        /// <param name="x">The absolute x coordinate for placement.</param>
        /// <param name="y">The absolute y coordinate for placement.</param>
        /// <param name="farmer">The <see cref="Farmer"/> placing the item.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the item can be placed by the farmer at the given
        ///     position at the provided coordinates. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool PlayerCanPlaceItem(GameLocation gameLocation, Item item, int x, int y, Farmer farmer)
        {
            if (Utility.isPlacementForbiddenHere(gameLocation))
            {
                return false;
            }

            if (item == null || item is Tool || Game1.eventUp || (bool)farmer.bathingClothes || farmer.onBridge.Value)
            {
                return false;
            }

            int tileX = x / Game1.tileSize;
            int tileY = y / Game1.tileSize;

            if (item is Furniture furniture && (!gameLocation.CanPlaceThisFurnitureHere(furniture) || !gameLocation.CanFreePlaceFurniture()))
            {
                return false;
            }

            Vector2 tileLocation = new Vector2(tileX, tileY);

            if (gameLocation.getObjectAtTile(tileX, tileY) is Fence fence && fence.CanRepairWithThisItem(item))
            {
                return true;
            }

            if (item.canBePlacedHere(gameLocation, tileLocation))
            {
                if (!((SObject)item).isPassable())
                {
                    foreach (Farmer otherFarmer in gameLocation.farmers)
                    {
                        if (otherFarmer.GetBoundingBox().Intersects(new Rectangle(x, y, Game1.tileSize, Game1.tileSize)))
                        {
                            return false;
                        }
                    }
                }

                if (ObjectsPatcher.ItemCanBePlaced(gameLocation, tileLocation, item) || Utility.isViableSeedSpot(gameLocation, tileLocation, item))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Method called via Harmony before <see cref="SObject.drawPlacementBounds"/>. It shows
        ///     points away from the farmer as available for placing the object.
        /// </summary>
        /// <param name="__instance">The <see cref="SObject"/> instance.</param>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        /// <param name="location">
        ///     The <see cref="GameLocation"/> where the object is being placed.
        /// </param>
        /// <returns>
        ///     Returns <see langword="false"/>, terminating prefixes and skipping the execution of
        ///     the original method, effectively replacing the original method.
        /// </returns>
        private static bool BeforeDrawPlacementBounds(SObject __instance, SpriteBatch spriteBatch, GameLocation location)
        {
            if (!__instance.isPlaceable() || __instance is Wallpaper)
            {
                return false;
            }

            int x = (int)Game1.GetPlacementGrabTile().X * Game1.tileSize;
            int y = (int)Game1.GetPlacementGrabTile().Y * Game1.tileSize;

            Game1.isCheckingNonMousePlacement = !Game1.IsPerformingMousePlacement();

            if (Game1.isCheckingNonMousePlacement)
            {
                Vector2 nearbyValidPlacementPosition = Utility.GetNearbyValidPlacementPosition(Game1.player, location, __instance, x, y);
                x = (int)nearbyValidPlacementPosition.X;
                y = (int)nearbyValidPlacementPosition.Y;
            }

            if (Utility.isThereAnObjectHereWhichAcceptsThisItem(location, __instance, x, y))
            {
                return false;
            }

            // bool canPlaceHere = Utility.playerCanPlaceItemHere(location, __instance, x, y,
            // Game1.player) || (Utility.isThereAnObjectHereWhichAcceptsThisItem(location,
            // __instance, x, y) && Utility.withinRadiusOfPlayer(x, y, 1, Game1.player));
            bool canPlaceHere = ObjectsPatcher.PlayerCanPlaceItem(location, __instance, x, y, Game1.player);

            Game1.isCheckingNonMousePlacement = false;

            int width = 1;
            int height = 1;

            if (__instance is Furniture furniture)
            {
                width = furniture.getTilesWide();
                height = furniture.getTilesHigh();
            }

            for (int x_offset = 0; x_offset < width; x_offset++)
            {
                for (int y_offset = 0; y_offset < height; y_offset++)
                {
                    spriteBatch.Draw(
                        Game1.mouseCursors,
                        new Vector2((((x / Game1.tileSize) + x_offset) * Game1.tileSize) - Game1.viewport.X, (((y / Game1.tileSize) + y_offset) * Game1.tileSize) - Game1.viewport.Y),
                        new Rectangle(canPlaceHere ? 194 : 210, 388, 16, 16),
                        Color.White,
                        0,
                        Vector2.Zero,
                        4,
                        SpriteEffects.None,
                        0.01f);
                }
            }

            if (__instance.bigCraftable.Value || __instance is Furniture || (__instance.Category != SObject.SeedsCategory && __instance.Category != SObject.fertilizerCategory))
            {
                __instance.draw(spriteBatch, x / Game1.tileSize, y / Game1.tileSize, 0.5f);
            }

            return false;
        }

        /// <summary>
        ///     A method called via Harmony before <see cref="Wallpaper.placementAction"/>. It
        ///     resets the <see cref="ClickToMove"/> object associated to the current game location.
        /// </summary>
        /// <param name="location">
        ///     The <see cref="GameLocation"/> where the wallpaper is being placed.
        /// </param>
        private static void BeforeWallpaperPlacementAction(GameLocation location)
        {
            ClickToMoveManager.GetOrCreate(location).Reset();
        }

        /// <summary>
        ///     This method is a copy of the private Utility.itemCanBePlaced. To be used by <see cref="TranspileDrawPlacementBounds"/>.
        /// </summary>
        /// <param name="gameLocation">The <see cref="Game"/> where the item is being placed.</param>
        /// <param name="tileLocation">The tile for placement.</param>
        /// <param name="item">The <see cref="Item"/> to place.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the item can be placed at the given tile in the
        ///     given <see cref="GameLocation"/>. retuens <see langword="false"/> otherwise.
        /// </returns>
        private static bool ItemCanBePlaced(GameLocation gameLocation, Vector2 tileLocation, Item item)
        {
            return
                gameLocation.isTilePlaceable(tileLocation, item)
                && item.isPlaceable()
                && (item.Category != SObject.SeedsCategory || (item is SObject svObject && svObject.isSapling()))
                && (((SObject)item).isPassable()
                    || !new Rectangle((int)(tileLocation.X * Game1.tileSize), (int)(tileLocation.Y * Game1.tileSize), Game1.tileSize, Game1.tileSize).Intersects(Game1.player.GetBoundingBox()));
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="SObject.drawPlacementBounds"/>. It
        ///     allows points away from the farmer to be shown as available for placing the object.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileDrawPlacementBounds(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code:
             *      bool flag = Utility.playerCanPlaceItemHere(location, this, num, num2, Game1.player) || (Utility.isThereAnObjectHereWhichAcceptsThisItem(location, this, num, num2) && Utility.withinRadiusOfPlayer(num, num2, 1, Game1.player));
             *          ...
             *          IL_007a: call bool StardewValley.Utility::playerCanPlaceItemHere(class StardewValley.GameLocation, class StardewValley.Item, int32, int32, class StardewValley.Farmer)
             *          ...
             *          IL_009f: stloc.2
             *
             * Replace with:
             *      bool flag = ObjectsPatcher.PlayerCanPlaceItem(location, __instance, num, num2, Game1.player);
             */

            MethodInfo playerCanPlaceItem = AccessTools.Method(typeof(ObjectsPatcher), nameof(ObjectsPatcher.PlayerCanPlaceItem));

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found1 = false;
            bool found2 = false;

            foreach (CodeInstruction instruction in codeInstructions)
            {
                if (!found1
                    && instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo { Name: "playerCanPlaceItemHere" })
                {
                    yield return new CodeInstruction(OpCodes.Call, playerCanPlaceItem);

                    found1 = true;

                    continue;
                }

                if (found1 && !found2)
                {
                    if (instruction.opcode != OpCodes.Stloc_2)
                    {
                        continue;
                    }

                    found2 = true;
                }

                yield return instruction;
            }

            if (!found1 || !found2)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(SObject)}.{nameof(SObject.drawPlacementBounds)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }
    }
}
