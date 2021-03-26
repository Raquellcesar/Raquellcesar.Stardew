// -----------------------------------------------------------------------
// <copyright file="ObjectsPatcher.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of __instance source code is governed by an MIT-style license that can be found in the
//     LICENSE file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Harmony;

    using Microsoft.Xna.Framework;
    using Microsoft.Xna.Framework.Graphics;

    using Raquellcesar.Stardew.Common;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Locations;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;

    using xTile.Dimensions;
    using xTile.ObjectModel;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     Encapsulates Harmony patches for the <see cref="Object"/> classes.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony naming rules.")]
    internal static class ObjectsPatcher
    {
        public static bool CanBePlaced(this SObject svObject, GameLocation gameLocation, int tileX, int tileY, Farmer farmer)
        {
            if (Utility.isThereAnObjectHereWhichAcceptsThisItem(gameLocation, svObject, tileX, tileY) && Utility.withinRadiusOfPlayer(tileX, tileY, 1, farmer))
            {
                return true;
            }

            if (Utility.isPlacementForbiddenHere(gameLocation) || Game1.eventUp || farmer.bathingClothes.Value || farmer.onBridge.Value)
            {
                return false;
            }

            if (gameLocation is AnimalHouse animalHouse && animalHouse.uniqueName.Value.Contains("Coop") && tileX == 2 && (tileY == 1 || tileY == 8 || tileY == 9))
            {
                return false;
            }

            Vector2 tileLocation = new Vector2(tileX, tileY);

            if (svObject.ParentSheetIndex == ObjectId.Bait && gameLocation.objects.TryGetValue(tileLocation, out SObject @object) && @object is not null && @object.ParentSheetIndex == ObjectId.CrabPot)
            {
                return true;
            }

            if (svObject.ParentSheetIndex == ObjectId.Beet || svObject.ParentSheetIndex == ObjectId.Wheat)
            {
                if (gameLocation is BuildableGameLocation)
                {
                    foreach (Building building in ((BuildableGameLocation)gameLocation).buildings)
                    {
                        if (building is Mill && tileLocation.X == building.tileX.Value + 1 && tileY == building.tileY.Value + 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            if (svObject.canBePlacedHere(gameLocation, tileLocation))
            {
                if (!svObject.isPassable())
                {
                    foreach (Farmer otherFarmer in gameLocation.farmers)
                    {
                        if (otherFarmer != farmer && otherFarmer.GetBoundingBox().Intersects(new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize)))
                        {
                            return false;
                        }
                    }
                }

                if (svObject.ParentSheetIndex == BigCraftableId.Tapper)
                {
                    if (gameLocation.terrainFeatures.ContainsKey(tileLocation) && gameLocation.terrainFeatures[tileLocation] is Tree tree && tree.growthStage.Value >= 5 && !tree.stump && !gameLocation.objects.ContainsKey(tileLocation))
                    {
                        return true;
                    }

                    return false;
                }

                if (gameLocation is FarmHouse farmHouse && farmHouse.upgradeLevel >= 2)
                {
                    if (tileY == 5 && (tileX == 22 || tileX == 2 || tileX == 26 || tileX == 27))
                    {
                        return false;
                    }
                }
                else if (svObject is Furniture furniture && svObject is not TV && furniture.furniture_type.Value == Furniture.decor && gameLocation is DecoratableLocation decoratableLocation)
                {
                    foreach (Furniture table in decoratableLocation.furniture)
                    {
                        if (table.name.Contains("Table") && tileLocation.X >= table.tileLocation.X && tileLocation.X < table.tileLocation.X + (float)(table.boundingBox.Width / 64) && tileLocation.Y >= table.tileLocation.Y && tileLocation.Y < table.tileLocation.Y + (float)(table.boundingBox.Height / 64))
                        {
                            return true;
                        }
                    }
                }

                if ((svObject.ParentSheetIndex == ObjectId.Sprinkler || svObject.ParentSheetIndex == ObjectId.QualitySprinkler || svObject.ParentSheetIndex == ObjectId.IridiumSprinkler)
                    && gameLocation.terrainFeatures.ContainsKey(tileLocation) && gameLocation.terrainFeatures[tileLocation] is HoeDirt)
                {
                    return true;
                }

                if (svObject.category.Value == SObject.SeedsCategory
                    && gameLocation.terrainFeatures.ContainsKey(tileLocation)
                    && gameLocation.terrainFeatures[tileLocation] is HoeDirt dirt
                    && dirt.canPlantThisSeedHere(svObject.ParentSheetIndex, tileX, tileY))
                {
                    return true;
                }

                if (svObject.name.Contains("Sapling"))
                {
                    Vector2 key = default;
                    for (int i = tileX - 2; i <= tileX + 2; i++)
                    {
                        for (int j = tileY - 2; j <= tileY + 2; j++)
                        {
                            key.X = i;
                            key.Y = j;
                            if (gameLocation.terrainFeatures.ContainsKey(key) && (gameLocation.terrainFeatures[key] is Tree || gameLocation.terrainFeatures[key] is FruitTree))
                            {
                                return false;
                            }
                        }
                    }

                    if (gameLocation.terrainFeatures.ContainsKey(tileLocation))
                    {
                        return gameLocation.terrainFeatures[tileLocation] is HoeDirt dirt1 && dirt1.crop is null;
                    }

                    if (gameLocation is not Farm || (gameLocation.doesTileHaveProperty(tileX, tileY, "Diggable", "Back") == null && !gameLocation.doesTileHavePropertyNoNull(tileX, tileY, "Type", "Back").Equals("Grass")) || gameLocation.doesTileHavePropertyNoNull(tileX, tileY, "NoSpawn", "Back").Equals("Tree"))
                    {
                        if (gameLocation.IsGreenhouse)
                        {
                            if (gameLocation.doesTileHaveProperty(tileX, tileY, "Diggable", "Back") == null)
                            {
                                return gameLocation.doesTileHavePropertyNoNull(tileX, tileY, "Type", "Back").Equals("Stone");
                            }

                            return true;
                        }

                        return false;
                    }

                    return true;
                }

                if (gameLocation.isTilePlaceable(tileLocation, svObject))
                {
                    if (svObject.ParentSheetIndex == ObjectId.CrabPot)
                    {
                        return ObjectsPatcher.CanPlaceCrabPot(gameLocation, tileX, tileY);
                    }

                    if (gameLocation.isTileOnMap(tileLocation) && gameLocation.isTilePassable(new Location(tileX, tileY), Game1.viewport))
                    {
                        if (!gameLocation.isTileOccupied(tileLocation)
                            || (gameLocation.terrainFeatures.ContainsKey(tileLocation)
                                && gameLocation.terrainFeatures[tileLocation] is HoeDirt
                                && (svObject.category.Value == SObject.SeedsCategory
                                    || svObject.category.Value == SObject.fertilizerCategory
                                    || svObject.ParentSheetIndex == ObjectId.CherryBomb
                                    || svObject.ParentSheetIndex == ObjectId.Bomb
                                    || svObject.ParentSheetIndex == ObjectId.MegaBomb)))
                        {
                            if (gameLocation is DecoratableLocation decoratableLocation)
                            {
                                foreach (Furniture furniture1 in decoratableLocation.furniture)
                                {
                                    if (furniture1.getBoundingBox(furniture1.tileLocation).Intersects(new Rectangle(tileX * Game1.tileSize, tileY * Game1.tileSize, Game1.tileSize, Game1.tileSize)))
                                    {
                                        return false;
                                    }
                                }

                                if (decoratableLocation.isTileOnWall(tileX, tileY)
                                    && (svObject.Category == SObject.BigCraftableCategory
                                        || svObject is not Furniture furniture
                                        || (furniture.furniture_type != Furniture.painting && furniture.furniture_type != Furniture.window)))
                                {
                                    return false;
                                }
                            }

                            return true;
                        }
                    }
                }

                if (gameLocation is DecoratableLocation && svObject.Category != SObject.BigCraftableCategory)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CanPlaceCrabPot(GameLocation gameLocation, int tileX, int tileY)
        {
            Vector2 tileVector = new Vector2(tileX, tileY);
            Location location = new Location(tileX, tileY);

            if (ObjectsPatcher.GetCrabPot(gameLocation, tileVector) is not null)
            {
                return false;
            }

            if (gameLocation.IsWaterTile(tileX, tileY))
            {
                if (gameLocation is Beach)
                {
                    if (!gameLocation.NeighboursLand(tileX, tileY))
                    {
                        return gameLocation.DistanceToNeighboursLand(tileX, tileY);
                    }

                    return false;
                }

                return gameLocation.NeighboursLand(tileX, tileY);
            }

            return false;
        }

        public static bool DistanceToNeighboursLand(this GameLocation gameLocation, int tileX, int tileY, int distance = 2)
        {
            int[] toAdd = new int[] { -distance, 0, distance };

            foreach (int x in toAdd)
            {
                foreach (int y in toAdd)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    if (gameLocation.IsNotWaterTileAndNotNullTile(tileX + x, tileY + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static CrabPot GetCrabPot(GameLocation gameLocation, Vector2 tileLocation)
        {
            if (gameLocation.objects.TryGetValue(tileLocation, out SObject @object) && @object is not null && @object.ParentSheetIndex == ObjectId.CrabPot)
            {
                return (CrabPot)@object;
            }

            return null;
        }

        /// <summary>
        ///     Initialize the Harmony patches.
        /// </summary>
        /// <param name="harmony">The Harmony patching API.</param>
        public static void Hook(HarmonyInstance harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(SObject), nameof(SObject.drawPlacementBounds)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeDrawPlacementBounds)));

            harmony.Patch(
                AccessTools.Method(typeof(Wallpaper), nameof(Wallpaper.placementAction)),
                new HarmonyMethod(typeof(ObjectsPatcher), nameof(ObjectsPatcher.BeforeWallpaperPlacementAction)));
        }

        public static bool IsNotWaterTileAndNotNullTile(this GameLocation gameLocation, int tileX, int tileY)
        {
            if (!gameLocation.IsWaterTile(tileX, tileY))
            {
                return gameLocation.map.GetLayer("Back").PickTile(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize), Game1.viewport.Size) is not null;
            }

            return false;
        }

        public static bool IsOnMap(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation.map.GetLayer("Back").PickTile(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize), Game1.viewport.Size) is not null;
        }

        public static bool IsWaterTile(this GameLocation gameLocation, int tileX, int tileY)
        {
            if (gameLocation.map.GetLayer("Back").PickTile(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize), Game1.viewport.Size) is Tile tile
                && tile.TileIndexProperties.TryGetValue("Water", out PropertyValue propertyValue))
            {
                if (propertyValue is not null)
                {
                    if (gameLocation.doesTileHaveProperty(tileX, tileY, "Passable", "Buildings") is not null)
                    {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        public static bool NeighboursLand(this GameLocation gameLocation, int tileX, int tileY)
        {
            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                int x = tileX + walkDirection.X;
                int y = tileY + walkDirection.Y;

                if (!gameLocation.IsWaterTile(x, y) && gameLocation.IsOnMap(x, y))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Method called via Harmony before <see
        ///     cref="StardewValley.Object.drawPlacementBounds"/>. It stops showing the red square
        ///     when the player selects a far point for placing the object.
        /// </summary>
        /// <param name="__instance">The <see cref="Object"/> instance.</param>
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

            //bool canPlaceHere = ObjectsPatcher.playerCanPlaceItemHere(location, __instance, x, y, Game1.player) || (Utility.isThereAnObjectHereWhichAcceptsThisItem(location, __instance, x, y) && Utility.withinRadiusOfPlayer(x, y, 1, Game1.player));
            bool canPlaceHere = __instance.CanBePlaced(location, x / Game1.tileSize, y / Game1.tileSize, Game1.player);

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
    }
}
