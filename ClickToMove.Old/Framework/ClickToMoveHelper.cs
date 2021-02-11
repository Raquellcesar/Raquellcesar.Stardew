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
        public static Rectangle GetDesertMerchantBounds(this Desert desert)
        {
            return new Rectangle(2112, 1280, 836, 280);
        }

        public static Vector2 PlayerOffsetPosition =>
            new Vector2(Game1.player.position.X + (Game1.tileSize / 2), Game1.player.position.Y + (Game1.tileSize / 2));

        public static Vector2 PlayerPositionOnScreen =>
            new Vector2(
                Game1.player.position.X + (Game1.tileSize / 2) - Game1.viewport.X,
                Game1.player.position.Y + (Game1.tileSize / 2) - Game1.viewport.Y);

        public static bool ClickedEggAtEggFestival(Point clickPoint)
        {
            if (Game1.CurrentEvent is not null && Game1.CurrentEvent.FestivalName == "Egg Festival")
            {
                foreach (Prop prop in Game1.CurrentEvent.festivalProps)
                {
                    if (ClickToMoveHelper.PropContainsPoint(prop, clickPoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the player was clicked.
        /// </summary>
        /// <param name="x">The x coordinate of the clicked position.</param>
        /// <param name="y">The y coordinate of the clicked position.</param>
        /// <returns>returns true if the player was clicked, false otherwise.</returns>
        public static bool ClickedOnFarmer(int x, int y)
        {
            return new Rectangle((int)Game1.player.position.X, (int)Game1.player.position.Y - 85, Game1.tileSize, 125)
                .Contains(x, y);
        }

        public static bool ContainsTravellingCart(GameLocation gameLocation, int pointX, int pointY)
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

        public static bool ContainsTravellingDesertShop(GameLocation gameLocation, int pointX, int pointY)
        {
            return gameLocation is Desert desert && desert.IsTravelingMerchantHere() && desert.GetDesertMerchantBounds().Contains(pointX, pointY);
        }

        public static int CountItemsExcludingNulls(Chest chest)
        {
            return chest.items.Count(item => item is not null);
        }

        public static float Distance(Point point1, Point point2)
        {
            float num1 = point1.X - point2.X;
            float num2 = point1.Y - point2.Y;
            return (float)Math.Sqrt((num1 * (double)num1) + (num2 * (double)num2));
        }

        public static FarmAnimal GetFarmAnimal(GameLocation gameLocation, int x, int y)
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

        public static int GetFishingAddedDistance(Farmer who)
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

        public static Furniture GetFurnitureClickedOn(GameLocation gameLocation, int clickPointX, int clickPointY)
        {
            return gameLocation is DecoratableLocation decoratableLocation
                       ? decoratableLocation.furniture.FirstOrDefault(
                           furniture => furniture.getBoundingBox(furniture.tileLocation.Value)
                               .Contains(clickPointX, clickPointY))
                       : null;
        }

        public static Point GetNearestTileNextToBuilding(AStarGraph graph, Building building)
        {
            int buildingTileX = building.tileX.Value;
            int buildingTileY = building.tileY.Value;
            int buildingTilesWidth = building.tilesWide.Value;
            int buildingTilesHeight = building.tilesHigh.Value;

            int tileX;
            int tileY;

            if (Game1.player.getTileX() < buildingTileX)
            {
                tileX = buildingTileX;
            }
            else if (Game1.player.getTileX() > buildingTileX + buildingTilesWidth - 1)
            {
                tileX = buildingTileX + buildingTilesWidth - 1;
            }
            else
            {
                tileX = Game1.player.getTileX();
            }

            if (Game1.player.getTileY() < buildingTileY)
            {
                tileY = buildingTileY;
            }
            else if (Game1.player.getTileY() > buildingTileY + buildingTilesHeight)
            {
                tileY = buildingTileY + buildingTilesHeight - 1;
            }
            else
            {
                tileY = Game1.player.getTileY();
            }

            Point tile = ClickToMoveHelper.GetTileNextToBuilding(tileX, tileY, graph);

            if (tile != Point.Zero)
            {
                return tile;
            }

            // There is no direct path to the nearest tile, let's search for an alternative around it.

            List<Point> tilesAroundBuilding = ClickToMoveHelper.ListOfTilesSurroundingBuilding(building);

            int tileIndex = 0;

            for (int i = 0; i < tilesAroundBuilding.Count; i++)
            {
                if (tilesAroundBuilding[i].X == tileX && tilesAroundBuilding[i].Y == tileY)
                {
                    tileIndex = i;
                    break;
                }
            }

            for (int i = 1, previousIndex = tileIndex - 1, nextIndex = tileIndex + 1;
                 i < tilesAroundBuilding.Count / 2;
                 i++, previousIndex--, nextIndex++)
            {
                if (previousIndex < 0)
                {
                    previousIndex += tilesAroundBuilding.Count;
                }

                tile = ClickToMoveHelper.GetTileNextToBuilding(
                    tilesAroundBuilding[previousIndex].X,
                    tilesAroundBuilding[previousIndex].Y,
                    graph);

                if (tile != Point.Zero)
                {
                    return tile;
                }

                if (nextIndex > tilesAroundBuilding.Count - 1)
                {
                    nextIndex -= tilesAroundBuilding.Count;
                }

                tile = ClickToMoveHelper.GetTileNextToBuilding(
                    tilesAroundBuilding[nextIndex].X,
                    tilesAroundBuilding[nextIndex].Y,
                    graph);

                if (tile != Point.Zero)
                {
                    return tile;
                }
            }

            return new Point(Game1.player.getTileX(), Game1.player.getTileY());
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

        public static bool IsBoulderAt(GameLocation gameLocation, int x, int y)
        {
            if (!(gameLocation is Forest || gameLocation is Woods) && gameLocation.resourceClumps.Any(
                    resourceClump => ClickToMoveHelper.IsResourceClumpBoulderAt(resourceClump, x, y)))
            {
                return true;
            }

            gameLocation.objects.TryGetValue(new Vector2(x, y), out SObject @object);

            return @object is not null && (@object.Name == "Stone" || @object.Name == "Boulder");
        }

        public static bool IsBushDestroyable(Bush bush, GameLocation gameLocation, Point tile)
        {
            if (bush.isDestroyable(gameLocation, new Vector2(tile.X, tile.Y)))
            {
                return true;
            }

            Type bushType = bush.GetType();

            return bushType.Name == "DestroyableBush" || bushType.BaseType?.Name == "DestroyableBush";
        }

        public static bool IsCropReadyToHarvest(Crop crop)
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

        public static bool IsMatureTreeStumpOrBoulderAt(GameLocation gameLocation, Point tile)
        {
            gameLocation.terrainFeatures.TryGetValue(new Vector2(tile.X, tile.Y), out TerrainFeature terrainFeature);

            if (terrainFeature is Tree || terrainFeature is FruitTree
                                       || (terrainFeature is Bush bush
                                           && ClickToMoveHelper.IsBushDestroyable(bush, gameLocation, tile)))
            {
                return true;
            }

            foreach (LargeTerrainFeature largeTerrainFeature in gameLocation.largeTerrainFeatures)
            {
                if (largeTerrainFeature is Bush bush2
                    && bush2.getRenderBounds(new Vector2(bush2.tilePosition.X, bush2.tilePosition.Y)).Contains(
                        tile.X * Game1.tileSize,
                        tile.Y * Game1.tileSize) && ClickToMoveHelper.IsBushDestroyable(bush2, gameLocation, tile))
                {
                    return true;
                }
            }

            return ClickToMoveHelper.IsStumpAt(gameLocation, tile.X, tile.Y)
                   || ClickToMoveHelper.IsBoulderAt(gameLocation, tile.X, tile.Y);
        }

        public static bool IsOreAt(GameLocation location, Point tile)
        {
            return location.orePanPoint.Value != Point.Zero && location.orePanPoint.X == tile.X
                                                            && location.orePanPoint.Y == tile.Y;
        }

        public static bool IsRockCrabHidingInShell(RockCrab rockCrab)
        {
            if (rockCrab.Sprite.currentFrame % 4 == 0)
            {
                return !ClickToMoveManager.Reflection.GetField<NetBool>(rockCrab, "shellGone").GetValue().Value;
            }

            return false;
        }

        /// <summary>
        ///     Checks if a fence is an isolated gate, i.e. a gate that has no neighbour fences.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation" /> where the fence is.</param>
        /// <param name="fence">The <see cref="Fence" /> to check.</param>
        /// <returns>Returns true if the given fence is a gate and has no other fences around it. Returns false otherwise.</returns>
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

        public static bool IsStumpAt(GameLocation gameLocation, int x, int y)
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
        ///     Returns true if the given tile is passable. Returns false otherwise.
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

        public static bool IsWizardBuilding(GameLocation gameLocation, Vector2 tile)
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

        public static bool NpcAtWarpOrDoor(NPC npc, GameLocation gameLocation)
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

        public static bool PlayerHasTool(string toolName)
        {
            return Game1.player.items.Any(item => item?.Name.Contains(toolName) == true);
        }

        public static void PlaySingingStone(SObject @object)
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

        public static bool PropContainsPoint(Prop prop, Point point)
        {
            Rectangle boundingRect = ClickToMoveManager.Reflection.GetField<Rectangle>(prop, "boundingRect").GetValue();
            return boundingRect.Contains(point.X, point.Y);
        }

        /// <summary>
        ///     Selects a tool by name.
        /// </summary>
        /// <param name="toolName">
        ///     The tool name.
        /// </param>
        /// <returns>
        ///     Returns true if the tool was found and selected in the player's inventory; false, otherwise.
        /// </returns>
        public static bool SelectTool(string toolName)
        {
            for (int i = 0; i < Game1.player.items.Count; i++)
            {
                if (Game1.player.items[i] is not null && Game1.player.items[i].Name.Contains(toolName))
                {
                    Game1.player.CurrentToolIndex = i;

                    return true;
                }
            }

            return false;
        }

        public static double SquaredEuclideanDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0);
        }

        public static bool WarpIfInRange(GameLocation gameLocation, Warp warp)
        {
            Vector2 warpVector = new Vector2(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

            if (Vector2.Distance(warpVector, Game1.player.position.Value) < ClickToMoveHelper.WarpRange(gameLocation))
            {
                Game1.player.warpFarmer(warp);
                return true;
            }

            return false;
        }

        public static bool WarpIfInRange(GameLocation gameLocation, Vector2 clickPoint)
        {
            float warpRange = ClickToMoveHelper.WarpRange(gameLocation);

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

        public static float WarpRange(GameLocation gameLocation)
        {
            if (gameLocation is not null && (gameLocation.isOutdoors.Value || gameLocation is BathHousePool))
            {
                return Game1.tileSize * 2;
            }

            return Game1.tileSize * 1.5f;
        }

        private static Point GetTileNextToBuilding(int tileX, int tileY, AStarGraph graph)
        {
            AStarNode tileNode = graph.GetNode(tileX, tileY);

            if (tileNode is not null)
            {
                tileNode.FakeTileClear = true;

                AStarPath path = graph.FindPathWithBubbleCheck(graph.FarmerNodeOffset, tileNode);

                if (path is not null && path.Count > 0)
                {
                    return new Point(tileX, tileY);
                }

                tileNode.FakeTileClear = false;
            }

            return Point.Zero;
        }

        private static bool IsResourceClumpBoulderAt(ResourceClump resourceClump, int x, int y)
        {
            return resourceClump.occupiesTile(x, y)
                   && (resourceClump.parentSheetIndex.Value == ResourceClump.meteoriteIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.boulderIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock1Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock2Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock3Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock4Index);
        }

        private static List<Point> ListOfTilesSurroundingBuilding(Building building)
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
    }
}