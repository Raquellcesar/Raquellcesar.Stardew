// ------------------------------------------------------------------------------------------------
// <copyright file="ObjectsPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Harmony;

    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Locations;
    using StardewValley.Objects;

    using xTile.Dimensions;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="SObject"/> classes.
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
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.canBePlacedHere)),
                transpiler: new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.TranspileCanBePlacedHere)));

            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.drawPlacementBounds)),
                transpiler: new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.TranspileDrawPlacementBounds)));

            harmony.Patch(
                AccessTools.Method(typeof(CrabPot), nameof(CrabPot.checkForAction)),
                transpiler: new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.TranspileCrabPotCheckForAction)));

            harmony.Patch(
                AccessTools.Method(typeof(Wallpaper), nameof(Wallpaper.placementAction)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeWallpaperPlacementAction)));
        }

        /// <summary>
        ///     Whether a tile in this <see cref="GameLocation"/> has a neighbour tile in land at a
        ///     given distance.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <param name="distance">The distance to the given tile. Defaults to 1.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile has a land neighbour in this <see
        ///     cref="GameLocation"/>. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool NeighboursLand(this GameLocation gameLocation, int tileX, int tileY, int distance = 1)
        {
            foreach (int deltaX in new List<int> { -distance, 0, distance })
            {
                foreach (int deltaY in new List<int> { -distance, 0, distance })
                {
                    if (deltaX != 0 || deltaY != 0)
                    {
                        int x = tileX + deltaX;
                        int y = tileY + deltaY;

                        if (gameLocation.map.GetLayer("Back").PickTile(new Location(x * Game1.tileSize, y * Game1.tileSize), Game1.viewport.Size) is not null
                            && (gameLocation.doesTileHaveProperty(x, y, "Water", "Back") is null
                                || gameLocation.doesTileHaveProperty(x, y, "Passable", "Buildings") is not null))
                        {
                            return true;
                        }
                    }
                }
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
        ///     This method is a copy of the private Utility.itemCanBePlaced ignoring The checks for
        ///     placement on the Farmer's tile. To be used by <see cref="TranspileDrawPlacementBounds"/>.
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
                    /*&& (((SObject)item).isPassable()
                        || !new Rectangle((int)(tileLocation.X * Game1.tileSize), (int)(tileLocation.Y * Game1.tileSize), Game1.tileSize, Game1.tileSize).Intersects(Game1.player.GetBoundingBox()))*/;
        }

        /// <summary>
        ///     This method is used instead of the original <see cref="Item.canBePlacedHere"/> in
        ///     <see cref="PlayerCanPlaceItem"/>. If the item is a <see cref="CrabPot"/>, it checks
        ///     for a land tile adjacent to the checked tile. Since now we check tiles away from the
        ///     player for placement we need to make this check to avoid having tiles in the middle
        ///     of water being considered available for placement of crab pots.
        /// </summary>
        /// <param name="item">The item to be placed.</param>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the tile is.</param>
        /// <param name="tile">The tile where to place the item.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the item can be placed at the given tile in the
        ///     given <see cref="GameLocation"/>. returns <see langword="false"/> otherwise.
        /// </returns>
        private static bool ItemCanBePlacedHere(Item item, GameLocation gameLocation, Vector2 tile)
        {
            if (item.ParentSheetIndex != ObjectId.CrabPot)
            {
                return item.canBePlacedHere(gameLocation, tile);
            }

            if (gameLocation is Caldera)
            {
                return false;
            }

            int x = (int)tile.X;
            int y = (int)tile.Y;

            if (gameLocation.objects.ContainsKey(tile)
                || gameLocation.doesTileHaveProperty(x, y, "Water", "Back") is null
                || gameLocation.doesTileHaveProperty(x, y, "Passable", "Buildings") is not null)
            {
                return false;
            }

            if ((gameLocation.doesTileHaveProperty(x + 1, y, "Water", "Back") is null || gameLocation.doesTileHaveProperty(x - 1, y, "Water", "Back") is null)
                && (gameLocation.doesTileHaveProperty(x, y + 1, "Water", "Back") is null || gameLocation.doesTileHaveProperty(x, y - 1, "Water", "Back") is null))
            {
                return false;
            }

            return gameLocation.NeighboursLand(x, y);
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
        private static bool PlayerCanPlaceItem(GameLocation gameLocation, Item item, int x, int y, Farmer farmer)
        {
            if (Utility.isPlacementForbiddenHere(gameLocation))
            {
                return false;
            }

            if (item == null || item is Tool || Game1.eventUp || farmer.bathingClothes.Value || farmer.onBridge.Value)
            {
                return false;
            }

            int tileX = x / Game1.tileSize;
            int tileY = y / Game1.tileSize;

            if (item is Furniture furniture
                && (!gameLocation.CanPlaceThisFurnitureHere(furniture)
                    || (!gameLocation.CanFreePlaceFurniture() && !furniture.IsCloseEnoughToFarmer(farmer, tileX, tileY))))
            {
                return false;
            }

            if (gameLocation.getObjectAtTile(tileX, tileY) is Fence fence && fence.CanRepairWithThisItem(item))
            {
                return true;
            }

            Vector2 tileLocation = new Vector2(tileX, tileY);

            if (ObjectsPatcher.ItemCanBePlacedHere(item, gameLocation, tileLocation))
            {
                if (!((SObject)item).isPassable())
                {
                    foreach (Farmer otherFarmer in gameLocation.farmers)
                    {
                        if (otherFarmer != Game1.player && otherFarmer.GetBoundingBox().Intersects(new Rectangle(x, y, Game1.tileSize, Game1.tileSize)))
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
        ///     A method called via Harmony to modify <see cref="SObject.canBePlacedHere"/>. It
        ///     removes the check for the Farmer occupying the placement position, since now the
        ///     Farmer automatically moves out of the way.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileCanBePlacedHere(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code:
             *     if (this.name != null && this.name.Contains("Bomb") && (!l.isTileOccupiedForPlacement(tile, this) || l.isTileOccupiedByFarmer(tile) != null))
             *         ...
             *         IL_00e4: ldarg.1
             *         IL_00e5: ldarg.2
             *         IL_00e6: callvirt instance class StardewValley.Farmer StardewValley.GameLocation::isTileOccupiedByFarmer(valuetype [Microsoft.Xna.Framework]Microsoft.Xna.Framework.Vector2)
             *         IL_00eb: brfalse.s IL_00ef
             *
             * Replace with:
             *     if (this.name != null && this.name.Contains("Bomb") && (!l.isTileOccupiedForPlacement(tile, this))
             */

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found
                    && codeInstructions[i].opcode == OpCodes.Ldarg_1
                    && i + 4 < codeInstructions.Count
                    && codeInstructions[i + 2].opcode == OpCodes.Callvirt
                    && codeInstructions[i + 2].operand is MethodInfo { Name: "isTileOccupiedByFarmer" }
                    && codeInstructions[i + 3].opcode == OpCodes.Brfalse)
                {
                    i += 4;

                    found = true;
                }

                yield return codeInstructions[i];
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(SObject)}.{nameof(SObject.canBePlacedHere)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
        }

        /// <summary>
        ///     A method called via Harmony to modify <see cref="CrabPot.checkForAction"/>. It
        ///     removes the check for a click when trying to remove the <see cref="CrabPot"/>.
        /// </summary>
        /// <param name="instructions">The method instructions to transpile.</param>
        private static IEnumerable<CodeInstruction> TranspileCrabPotCheckForAction(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             * Relevant CIL code to remove:
             *      if (Game1.didPlayerJustClickAtAll(ignoreNonMouseHeldInput: true))
             *          IL_018a: ldc.i4.1
             *          IL_018b: call bool StardewValley.Game1::didPlayerJustClickAtAll(bool)
             *          IL_0190: brfalse.s IL_01ee
             */

            List<CodeInstruction> codeInstructions = instructions.ToList();

            bool found = false;

            for (int i = 0; i < codeInstructions.Count; i++)
            {
                if (!found && codeInstructions[i].opcode == OpCodes.Ldc_I4_1 && i + 3 < codeInstructions.Count && codeInstructions[i + 1].opcode == OpCodes.Call && codeInstructions[i + 1].operand is MethodInfo { Name: "didPlayerJustClickAtAll" } && codeInstructions[i + 2].opcode == OpCodes.Brfalse)
                {
                    codeInstructions[i + 3].labels.AddRange(codeInstructions[i].labels);
                    i += 3;

                    found = true;
                }

                yield return codeInstructions[i];
            }

            if (!found)
            {
                ClickToMoveManager.Monitor.Log(
                    $"Failed to patch {nameof(CrabPot)}.{nameof(CrabPot.checkForAction)}.\nThe point of injection was not found.",
                    LogLevel.Error);
            }
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

            bool found1 = false;
            bool found2 = false;

            foreach (CodeInstruction instruction in instructions)
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
