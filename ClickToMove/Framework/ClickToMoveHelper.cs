// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMoveHelper.cs">
//   Copyright (c) 2021 Raquellcesar
//
//   Use of this source code is governed by an MIT-style license that can be found in the LICENSE file
//   or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Xna.Framework;

    using Netcode;

    using Raquellcesar.Stardew.ClickToMove.Framework.PathFinding;
    using Raquellcesar.Stardew.Common;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Locations;
    using StardewValley.Minigames;
    using StardewValley.Monsters;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;
    using StardewValley.Tools;

    using xTile.Dimensions;
    using xTile.ObjectModel;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>Provides methods for interacting with the game code.</summary>
    internal static class ClickToMoveHelper
    {
        public static Vector2 PlayerOffsetPosition =>
            new Vector2(Game1.player.position.X + (Game1.tileSize / 2), Game1.player.position.Y + (Game1.tileSize / 2));

        public static Vector2 PlayerPositionOnScreen =>
            new Vector2(
                Game1.player.position.X + (Game1.tileSize / 2) - Game1.viewport.X,
                Game1.player.position.Y + (Game1.tileSize / 2) - Game1.viewport.Y);

        public static bool AtWarpOrDoor(this NPC npc, GameLocation gameLocation)
        {
            if (gameLocation.isCollidingWithWarp(npc.GetBoundingBox(), npc) is not null)
            {
                return true;
            }

            PropertyValue value = null;
            gameLocation.map.GetLayer("Buildings").PickTile(npc.nextPositionPoint(), Game1.viewport.Size)?.Properties
                .TryGetValue("Action", out value);
            return value is not null;
        }

        public static bool ClickedEggAtEggFestival(Point clickPoint)
        {
            if (Game1.CurrentEvent is not null && Game1.CurrentEvent.FestivalName == "Egg Festival")
            {
                foreach (Prop prop in Game1.CurrentEvent.festivalProps)
                {
                    if (prop.ContainsPoint(clickPoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether this farmer was clicked.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <param name="x">The x coordinate of the clicked position.</param>
        /// <param name="y">The y coordinate of the clicked position.</param>
        /// <returns>Returns <see langword="true"/> if the player was clicked, false otherwise.</returns>
        public static bool ClickedOn(this Farmer farmer, int x, int y)
        {
            return new Rectangle((int)farmer.position.X, (int)farmer.position.Y - 85, Game1.tileSize, 125)
                .Contains(x, y);
        }

        public static bool ContainsPoint(this Prop prop, Point point)
        {
            Rectangle boundingRect = ClickToMoveManager.Reflection.GetField<Rectangle>(prop, "boundingRect").GetValue();
            return boundingRect.Contains(point.X, point.Y);
        }

        public static bool ContainsTravellingCart(this GameLocation gameLocation, int pointX, int pointY)
        {
            if (gameLocation is Forest { travelingMerchantBounds: { } } forest)
            {
                foreach (Rectangle travelingMerchantBounds in forest.travelingMerchantBounds)
                {
                    if (travelingMerchantBounds.Contains(pointX, pointY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ContainsTravellingDesertShop(this GameLocation gameLocation, int pointX, int pointY)
        {
            return gameLocation is Desert desert && desert.IsTravelingMerchantHere() && desert.GetDesertMerchantBounds().Contains(pointX, pointY);
        }

        public static int CountNonNullItems(this Chest chest)
        {
            return chest.items.Count(item => item is not null);
        }

        public static float Distance(Point point1, Point point2)
        {
            float num1 = point1.X - point2.X;
            float num2 = point1.Y - point2.Y;
            return (float)Math.Sqrt((num1 * (double)num1) + (num2 * (double)num2));
        }

        public static Rectangle GetDesertMerchantBounds(this Desert desert)
        {
            return new Rectangle(2112, 1280, 836, 280);
        }

        public static FarmAnimal GetFarmAnimal(this GameLocation gameLocation, int x, int y)
        {
            if (gameLocation is AnimalHouse animalHouse)
            {
                foreach (FarmAnimal farmAnimal in animalHouse.animals.Values)
                {
                    if (farmAnimal.GetBoundingBox().Contains(x, y))
                    {
                        return farmAnimal;
                    }
                }
            }

            if (gameLocation is Farm farm)
            {
                foreach (FarmAnimal farmAnimal in farm.animals.Values)
                {
                    if (farmAnimal.GetBoundingBox().Contains(x, y))
                    {
                        return farmAnimal;
                    }
                }
            }

            return null;
        }

        public static int GetFishingAddedDistance(this Farmer who)
        {
            if (who.FishingLevel >= 15)
            {
                return 4;
            }

            if (who.FishingLevel >= 8)
            {
                return 3;
            }

            if (who.FishingLevel >= 4)
            {
                return 2;
            }

            if (who.FishingLevel >= 1)
            {
                return 1;
            }

            return 0;
        }

        public static Furniture GetFurniture(this GameLocation gameLocation, int clickPointX, int clickPointY)
        {
            return gameLocation is DecoratableLocation decoratableLocation
                       ? decoratableLocation.furniture.FirstOrDefault(
                           furniture => furniture.getBoundingBox(furniture.tileLocation.Value)
                               .Contains(clickPointX, clickPointY))
                       : null;
        }

        public static Point GetNextPointOut(int startX, int startY, int endX, int endY)
        {
            Point result = new Point(endX, endY);

            if (startX < endX)
            {
                result.X--;
            }
            else if (startX > endX)
            {
                result.X++;
            }

            if (startY < endY)
            {
                result.Y--;
            }
            else if (startY > endY)
            {
                result.Y++;
            }

            return result;
        }

        public static bool HasTool(this Farmer farmer, string toolName)
        {
            return farmer.items.Any(item => item?.Name.Contains(toolName) == true);
        }

        public static bool HoeSelectedAndTileHoeable(GameLocation gameLocation, Point tile)
        {
            if (Game1.player.CurrentTool is Hoe
                && gameLocation.doesTileHaveProperty(tile.X, tile.Y, "Diggable", "Back") is not null
                && !gameLocation.isTileOccupied(new Vector2(tile.X, tile.Y)))
            {
                return gameLocation.isTilePassable(new Location(tile.X, tile.Y), Game1.viewport);
            }

            return false;
        }

        public static bool InMiniGameWhereWeDontWantClicks()
        {
            if (Game1.currentMinigame is not null)
            {
                return Game1.currentMinigame is AbigailGame || Game1.currentMinigame is FantasyBoardGame
                                                            || Game1.currentMinigame is GrandpaStory
                                                            || Game1.currentMinigame is HaleyCowPictures
                                                            || Game1.currentMinigame is MineCart
                                                            || Game1.currentMinigame is PlaneFlyBy
                                                            || Game1.currentMinigame is RobotBlastoff;
            }

            return false;
        }

        public static bool IsBoulderAt(this GameLocation gameLocation, int x, int y)
        {
            if (!(gameLocation is Forest || gameLocation is Woods) && gameLocation.resourceClumps.Any(
                    resourceClump => ClickToMoveHelper.IsBoulderAt(resourceClump, x, y)))
            {
                return true;
            }

            gameLocation.objects.TryGetValue(new Vector2(x, y), out SObject @object);

            return @object is not null && (@object.Name == "Stone" || @object.Name == "Boulder");
        }

        public static bool IsDestroyable(this Bush bush, GameLocation gameLocation, Point tile)
        {
            if (bush.isDestroyable(gameLocation, new Vector2(tile.X, tile.Y)))
            {
                return true;
            }

            Type bushType = bush.GetType();

            return bushType.Name == "DestroyableBush" || bushType.BaseType?.Name == "DestroyableBush";
        }

        public static bool IsHidingInShell(this RockCrab rockCrab)
        {
            if (rockCrab.Sprite.currentFrame % 4 == 0)
            {
                return !ClickToMoveManager.Reflection.GetField<NetBool>(rockCrab, "shellGone").GetValue().Value;
            }

            return false;
        }

        public static bool IsMatureTreeStumpOrBoulderAt(this GameLocation gameLocation, Point tile)
        {
            gameLocation.terrainFeatures.TryGetValue(new Vector2(tile.X, tile.Y), out TerrainFeature terrainFeature);

            if (terrainFeature is Tree || terrainFeature is FruitTree
                                       || (terrainFeature is Bush bush
                                           && bush.IsDestroyable(gameLocation, tile)))
            {
                return true;
            }

            foreach (LargeTerrainFeature largeTerrainFeature in gameLocation.largeTerrainFeatures)
            {
                if (largeTerrainFeature is Bush bush2
                    && bush2.getRenderBounds(new Vector2(bush2.tilePosition.X, bush2.tilePosition.Y)).Contains(
                        tile.X * Game1.tileSize,
                        tile.Y * Game1.tileSize) && bush2.IsDestroyable(gameLocation, tile))
                {
                    return true;
                }
            }

            return gameLocation.IsStumpAt(tile.X, tile.Y)
                   || gameLocation.IsBoulderAt(tile.X, tile.Y);
        }

        public static bool IsOreAt(this GameLocation location, Point tile)
        {
            return location.orePanPoint.Value != Point.Zero && location.orePanPoint.X == tile.X
                                                            && location.orePanPoint.Y == tile.Y;
        }

        public static bool IsReadyToHarvest(this Crop crop)
        {
            if (crop.currentPhase.Value >= crop.phaseDays.Count - 1)
            {
                if (crop.fullyGrown.Value)
                {
                    return crop.dayOfCurrentPhase.Value <= 0;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks if a fence is an isolated gate, i.e. a gate that has no neighbour fences.
        ///     Such fences can't be open and therefore are impassable.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation" /> where the fence is.</param>
        /// <param name="fence">The <see cref="Fence" /> to check.</param>
        /// <returns>Returns <see langword="true"/> if the given fence is a gate and has no other fences around it. Returns <see langword="false"/> otherwise.</returns>
        public static bool IsSoloGate(this GameLocation gameLocation, Fence fence)
        {
            foreach (WalkDirection walkDirection in WalkDirection.SimpleDirections)
            {
                gameLocation.objects.TryGetValue(
                    new Vector2(fence.tileLocation.X + walkDirection.X, fence.tileLocation.Y + walkDirection.Y),
                    out SObject @object);

                if (@object is Fence neighbourFence && neighbourFence.countsForDrawing(fence.whichType.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsStumpAt(this GameLocation gameLocation, int x, int y)
        {
            if (gameLocation is Woods woods)
            {
                return woods.stumps.Any(
                    stump => stump.occupiesTile(x, y)
                             && (stump.parentSheetIndex.Value == ResourceClump.stumpIndex
                                 || stump.parentSheetIndex.Value == ResourceClump.hollowLogIndex));
            }

            if (gameLocation is not MineShaft && gameLocation is not Forest)
            {
                return gameLocation.resourceClumps.Any(
                    resourceClump => resourceClump.occupiesTile(x, y)
                                     && (resourceClump.parentSheetIndex.Value == ResourceClump.stumpIndex
                                         || resourceClump.parentSheetIndex.Value == ResourceClump.hollowLogIndex));
            }

            return false;
        }

        public static bool IsTilePassable(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation.IsTilePassable(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize));
        }

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation" /> to check.</param>
        /// <param name="location">The tile position in pixels.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile is passable. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsTilePassable(this GameLocation gameLocation, Location location)
        {
            Tile backTile = gameLocation.map.GetLayer("Back").PickTile(location, Game1.viewport.Size);

            if (backTile is null)
            {
                return false;
            }

            backTile.TileIndexProperties.TryGetValue("Passable", out PropertyValue propertyValue);
            if (propertyValue is not null)
            {
                return false;
            }

            Tile buildingTile = gameLocation.map.GetLayer("Buildings").PickTile(location, Game1.viewport.Size);
            if (buildingTile is not null)
            {
                buildingTile.TileIndexProperties.TryGetValue("Passable", out propertyValue);

                if (propertyValue is not null)
                {
                    return true;
                }

                buildingTile.TileIndexProperties.TryGetValue("Shadow", out propertyValue);

                return propertyValue is not null;
            }

            backTile.TileIndexProperties.TryGetValue("Water", out propertyValue);

            if (propertyValue is not null)
            {
                return false;
            }

            backTile.TileIndexProperties.TryGetValue("WaterSource", out propertyValue);

            return propertyValue is null;
        }

        /// <summary>
        ///     Checks if the travelling merchant is present at the desert.
        /// </summary>
        /// <param name="desert">The <see cref="Desert" /> instance.</param>
        /// <returns></returns>
        public static bool IsTravelingMerchantHere(this Desert desert)
        {
            if (Game1.currentSeason == "winter" && Game1.dayOfMonth >= 15)
            {
                return Game1.dayOfMonth > 17;
            }

            return true;
        }

        public static bool IsWater(this GameLocation location, Point tile)
        {
            if (location is Submarine && tile.X >= 9 && tile.X <= 20 && tile.Y >= 7 && tile.Y <= 11)
            {
                return true;
            }

            if (location.doesTileHaveProperty(tile.X, tile.Y, "Water", "Back") is null)
            {
                return location.doesTileHaveProperty(tile.X, tile.Y, "WaterSource", "Back") is not null;
            }

            return true;
        }

        public static bool IsWateringCanFillingSource(this GameLocation gameLocation, Point tile)
        {
            if (gameLocation.IsWater(tile) && !gameLocation.IsTilePassable(tile.X, tile.Y))
            {
                return true;
            }

            if (gameLocation is BuildableGameLocation buildableGameLocation)
            {
                Building building = buildableGameLocation.getBuildingAt(new Vector2(tile.X, tile.Y));
                if (building is not null && (building is FishPond || building.buildingType.Equals("Well"))
                                     && building.daysOfConstructionLeft.Value <= 0)
                {
                    return true;
                }
            }

            if (gameLocation is Submarine && tile.X >= 9 && tile.X <= 20 && tile.Y >= 7 && tile.Y <= 11)
            {
                return true;
            }

            if (gameLocation.name.Value == "Greenhouse"
                && ((tile.X == 9 && tile.Y == 7) || (tile.X == 10 && tile.Y == 7)))
            {
                return true;
            }

            if (gameLocation.name.Value == "Railroad" && tile.X >= 14 && tile.X <= 16 && tile.Y >= 55 && tile.Y <= 56)
            {
                return true;
            }

            return false;
        }

        public static bool IsWizardBuilding(this GameLocation gameLocation, Vector2 tile)
        {
            if (gameLocation is BuildableGameLocation buildableGameLocation)
            {
                Building buildingAt = buildableGameLocation.getBuildingAt(tile);

                if (buildingAt is not null && (buildingAt.buildingType.Contains("Obelisk")
                                           || buildingAt.buildingType == "Junimo Hut"))
                {
                    return true;
                }
            }

            return false;
        }

        public static List<Point> ListOfSurroundingTiles(this Building building)
        {
            List<Point> list = new List<Point>();

            int buildingTileX = building.tileX.Value;
            int buildingTileY = building.tileY.Value;
            int buildingTilesWidth = building.tilesWide.Value;
            int buildingTilesHeight = building.tilesHigh.Value;

            for (int i = 0, x = buildingTileX; i < buildingTilesWidth; i++, x++)
            {
                list.Add(new Point(x, buildingTileY));
            }

            int rightmostX = buildingTileX + buildingTilesWidth - 1;
            for (int i = 1, y = buildingTileY + 1; i < buildingTilesHeight; i++, y++)
            {
                list.Add(new Point(rightmostX, y));
            }

            int downmostY = buildingTileY + buildingTilesHeight - 1;
            for (int i = 1, x = rightmostX - 1; i < buildingTilesWidth; i++, x--)
            {
                list.Add(new Point(x, downmostY));
            }

            for (int i = 1, y = downmostY - 1; i < buildingTilesHeight - 1; i++, y--)
            {
                list.Add(new Point(buildingTileX, y));
            }

            return list;
        }

        public static void PlaySingingStone(this SObject @object)
        {
            if (Game1.soundBank is not null)
            {
                @object.shakeTimer = 100;

                ICue cue = Game1.soundBank.GetCue("crystal");
                int pitch = Game1.random.Next(2400);
                pitch -= pitch % 100;
                cue.SetVariable("Pitch", pitch);
                cue.Play();
            }
        }

        /// <summary>
        ///     Selects a tool by name.
        /// </summary>
        /// <param name="toolName">
        ///     The tool name.
        /// </param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tool was found and selected in the player's inventory; false, otherwise.
        /// </returns>
        public static bool SelectTool(this Farmer farmer, string toolName)
        {
            for (int i = 0; i < farmer.items.Count; i++)
            {
                if (farmer.items[i] is not null && farmer.items[i].Name.Contains(toolName))
                {
                    farmer.CurrentToolIndex = i;

                    return true;
                }
            }

            return false;
        }

        public static double SquaredEuclideanDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0);
        }

        public static bool WarpIfInRange(this GameLocation gameLocation, Warp warp)
        {
            Vector2 warpVector = new Vector2(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

            if (Vector2.Distance(warpVector, Game1.player.position.Value) < gameLocation.WarpRange())
            {
                Game1.player.warpFarmer(warp);
                return true;
            }

            return false;
        }

        public static bool WarpIfInRange(this GameLocation gameLocation, Vector2 clickPoint)
        {
            float warpRange = gameLocation.WarpRange();

            foreach (Warp warp in gameLocation.warps)
            {
                Vector2 warpVector = new Vector2(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

                if (Vector2.Distance(warpVector, clickPoint) < warpRange
                    && Vector2.Distance(warpVector, Game1.player.position.Value) < warpRange)
                {
                    Game1.player.warpFarmer(warp);

                    return true;
                }
            }

            return false;
        }

        public static float WarpRange(this GameLocation gameLocation)
        {
            if (gameLocation is not null && (gameLocation.isOutdoors.Value || gameLocation is BathHousePool))
            {
                return Game1.tileSize * 2;
            }

            return Game1.tileSize * 1.5f;
        }

        private static bool IsBoulderAt(this ResourceClump resourceClump, int x, int y)
        {
            return resourceClump.occupiesTile(x, y)
                   && (resourceClump.parentSheetIndex.Value == ResourceClump.meteoriteIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.boulderIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock1Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock2Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock3Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock4Index);
        }
    }
}
