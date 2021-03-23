// -----------------------------------------------------------------------
// <copyright file="ClickToMoveHelper.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Xna.Framework;

    using Netcode;

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
        /// <summary>
        ///     This <see cref="Farmer"/> position on map considering an offset of half tile.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <returns>This farmer's position on map considering an offset of half tile.</returns>
        public static Vector2 OffsetPositionOnMap(this Farmer farmer)
        {
            return new Vector2(farmer.position.X + (Game1.tileSize / 2), farmer.position.Y + (Game1.tileSize / 2));
        }

        /// <summary>
        ///     This <see cref="Farmer"/> position on screen considering an offset of half tile.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <returns>This farmer's position on screen considering an offset of half tile.</returns>
        public static Vector2 OffsetPositionOnScreen(this Farmer farmer)
        {
            return new Vector2(farmer.position.X + (Game1.tileSize / 2) - Game1.viewport.X, farmer.position.Y + (Game1.tileSize / 2) - Game1.viewport.Y);
        }

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
        /// <returns>Returns <see langword="true"/> if the farmer was clicked, false otherwise.</returns>
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

        /// <summary>
        ///     Checks if the travelling cart is occupying the given tile coordinates in the given
        ///     game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the travelling cart is occupying the given tile coordinates in the
        ///     given game location. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool ContainsTravellingCart(this GameLocation gameLocation, int tileX, int tileY)
        {
            if (gameLocation is Forest { travelingMerchantBounds: { } } forest)
            {
                foreach (Rectangle travelingMerchantBounds in forest.travelingMerchantBounds)
                {
                    if (travelingMerchantBounds.Contains(tileX, tileY))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if the travelling desert shop is occupying the given tile coordinates in the
        ///     given game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if the travelling desert shop is occupying the given tile coordinates
        ///     in the given game location. Returns false otherwise.
        /// </returns>
        public static bool ContainsTravellingDesertShop(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation is Desert desert && desert.IsTravelingMerchantHere() && desert.GetDesertMerchantBounds().Contains(tileX, tileY);
        }

        public static int CountNonNullItems(this Chest chest)
        {
            return chest.items.Count(item => item is not null);
        }

        /// <summary>
        ///     Gets the Euclidean distance between two points.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <returns>
        ///     Returns the Euclidean distance between the two given points.
        /// </returns>
        public static float Distance(Point point1, Point point2)
        {
            double num1 = point1.X - point2.X;
            double num2 = point1.Y - point2.Y;
            return (float)Math.Sqrt((num1 * num1) + (num2 * num2));
        }

        /// <summary>
        ///     Gets the Bounding box for the Desert Merchant.
        /// </summary>
        /// <param name="desert">The <see cref="Desert"/> instance.</param>
        /// <returns>The Bounding box for the Desert Merchant.</returns>
        public static Rectangle GetDesertMerchantBounds(this Desert desert)
        {
            return new Rectangle(2112, 1280, 836, 280);
        }

        /// <summary>
        ///     Gets the farm animal at the tile associated with this node, if there's one.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="x">The tile x coordinate.</param>
        /// <param name="y">The tile y coordinate.</param>
        /// <returns>
        ///     Returns the farm animal at the tile associated with this node, if there's one.
        ///     Returns null if there isn't any animal at the tile.
        /// </returns>
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

        /// <summary>
        ///     Checks whether this <see cref="Farmer"/> has the given tool in their inventory.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <param name="toolName">The tool name.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this <see cref="Farmer"/> has the given tool in their inventory.
        ///     Return <see langword="false"/> otherwise.
        /// </returns>
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

        /// <summary>
        ///     Checks if there's a boulder at a tile in a game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="x">The tile x coordinate.</param>
        /// <param name="y">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a boulder at the given tile in this game location.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsBoulderAt(this GameLocation gameLocation, int x, int y)
        {
            if (!(gameLocation is Forest || gameLocation is Woods) && gameLocation.resourceClumps.Any(
                    resourceClump => resourceClump.IsBoulderAt(x, y)))
            {
                return true;
            }

            gameLocation.objects.TryGetValue(new Vector2(x, y), out SObject @object);

            return @object is not null && (@object.Name == "Stone" || @object.Name == "Boulder");
        }

        /// <summary>
        ///     Checks whether a bush is destroyable from a given tile. Extends the game's <see
        ///     cref="Bush.isDestroyable"/> method to deal with bushes created by the Deep Woods mod.
        /// </summary>
        /// <param name="bush">The <see cref="Bush"/> instance.</param>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the bush is.</param>
        /// <param name="tile">The tile to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the bush can be destroyed by the player,
        ///     <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsDestroyable(this Bush bush, GameLocation gameLocation, Point tile)
        {
            if (bush.isDestroyable(gameLocation, new Vector2(tile.X, tile.Y)))
            {
                return true;
            }

            // Test for bushes from the mod Deep Woods.
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

        /// <summary>
        ///     Checks if there's a tree stump at a tile in a game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="x">The tile x coordinate.</param>
        /// <param name="y">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if there's a tree stump at the given tile in this game location.
        ///     Returns false otherwise.
        /// </returns>
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

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation" /> to check.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile is passable. Returns <see langword="false"/> otherwise.
        /// </returns>
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
        ///     Checks if the travelling merchant is present at this desert.
        /// </summary>
        /// <param name="desert">The <see cref="Desert"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the travelling merchant is present at the desert;
        ///     returns <see langword="false"/> otherwise.
        /// </returns>
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

        /// <summary>
        ///     Returns a list of the tiles at the border of this building.
        /// </summary>
        /// <param name="building">The <see cref="Building"/> instance.</param>
        /// <returns>A list of the tiles at the border of this building.</returns>
        public static List<Point> GetBorderTilesList(this Building building)
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
        ///     This <see cref="Farmer"/> selects a tool by name.
        /// </summary>
        /// <param name="farmer">This <see cref="Farmer"/> instance.</param>
        /// <param name="toolName"> The tool's name.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tool was found and selected in the farmer's inventory; <see langword="false"/>, otherwise.
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

        /// <summary>
        ///     Returns the squared Euclidean distance between two points.
        /// </summary>
        /// <param name="x1">The x coordinate of the first point.</param>
        /// <param name="y1">The y coordinate of the first point.</param>
        /// <param name="x2">The x coordinate of the second point.</param>
        /// <param name="y2">The y coordinate of the second point.</param>
        /// <returns>The squared Euclidean distance between two points.</returns>
        public static double SquaredEuclideanDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Pow(x1 - x2, 2.0) + Math.Pow(y1 - y2, 2.0);
        }

        /// <summary>
        ///     Warps the player through the given <see cref="Warp"/> if it's in range.
        /// </summary>
        /// <param name="gameLocation">The current game location.</param>
        /// <param name="warp">The warp to use.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the player is successfully warped.
        ///     return <see langword="false"/> otherwise.
        /// </returns>
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

        /// <summary>
        ///     Warps the player through a <see cref="Warp"/> that's in range of both the player
        ///     and the clicked point, if such a warp exists.
        /// </summary>
        /// <param name="gameLocation">The current game location.</param>
        /// <param name="clickPoint">The point clicked.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the player is successfully warped.
        ///     return <see langword="false"/> otherwise.
        /// </returns>
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
            if (gameLocation is not null && (gameLocation.IsOutdoors || gameLocation is BathHousePool))
            {
                return Game1.tileSize * 2;
            }

            return Game1.tileSize * 1.5f;
        }

        /// <summary>
        ///     Checks if a resource clump is a boulder occupying a given tile.
        /// </summary>
        /// <param name="resourceClump">The <see cref="ResourceClump"/> instance.</param>
        /// <param name="x">The tile x coordinate.</param>
        /// <param name="y">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if this <see cref="ResourceClump"/> is a boulder at the given tile
        ///     coordinates. Returns false otherwise.
        /// </returns>
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
