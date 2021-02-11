// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="ClickToMoveHelper.cs">
//     Copyright (c) 2021 Raquellcesar Use of this source code is governed by an MIT-style license
//     that can be found in the LICENSE file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework
{
    using Microsoft.Xna.Framework;

    using Netcode;

    using Raquellcesar.Stardew.Common;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Locations;
    using StardewValley.Minigames;
    using StardewValley.Monsters;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using xTile.Dimensions;
    using xTile.ObjectModel;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     Provides methods for interacting with the game code.
    /// </summary>
    internal static class ClickToMoveHelper
    {
        private static readonly string[] FestivalNames =
                    {
                "eggFestival", "flowerFestival", "luau", "jellies", "fair", "iceFestival"
            };

        /// <summary>
        ///     Encapsulates monitoring and logging.
        /// </summary>
        private static IMonitor monitor;

        /// <summary>
        ///     Simplifies access to private game code.
        /// </summary>
        private static IReflectionHelper reflection;

        /// <summary>
        ///     Gets whether we're in a minigame where we shouldn't have clicks.
        /// </summary>
        public static bool InMiniGameWhereWeDontWantClicks
        {
            get
            {
                return Game1.currentMinigame is AbigailGame or FantasyBoardGame or GrandpaStory or HaleyCowPictures or MineCart or PlaneFlyBy or RobotBlastoff;
            }
        }

        public static bool CanReallyBePlacedHere(
            this Wallpaper wallpaper,
            DecoratableLocation location,
            Vector2 tileLocation)
        {
            int x = (int)tileLocation.X;
            int y = (int)tileLocation.Y;

            if (wallpaper.isFloor.Value)
            {
                List<Rectangle> floors = location.getFloors();
                for (int i = 0; i < floors.Count; i++)
                {
                    if (floors[i].Contains(x, y))
                    {
                        return true;
                    }
                }
            }
            else
            {
                List<Rectangle> walls = location.getWalls();
                for (int j = 0; j < walls.Count; j++)
                {
                    if (walls[j].Contains(x, y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ClickedEggAtEggFestival(Vector2 clickPoint)
        {
            if (Game1.CurrentEvent is not null && Game1.CurrentEvent.FestivalName == "Egg Festival")
            {
                foreach (Prop prop in Game1.CurrentEvent.festivalProps)
                {
                    Rectangle boundingBox = ClickToMoveHelper.reflection.GetField<Rectangle>(prop, "boundingRect").GetValue();
                    if (boundingBox.Contains((int)clickPoint.X, (int)clickPoint.Y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if the Cinema's door is at the given tile coordinates.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if the Cinema's door is at the given tile coordinates. Returns false otherwise.
        /// </returns>
        public static bool ContainsCinemaDoor(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation is Town && (tileX == 52 || tileX == 53) && tileY >= 18 && tileY <= 19;
        }

        /// <summary>
        ///     Checks if the Cinema's ticket office is at the given tile coordinates.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if the Cinema's ticket office is at the given tile coordinates. Returns
        ///     false otherwise.
        /// </returns>
        public static bool ContainsCinemaTicketOffice(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation is Town && tileX >= 54 && tileX <= 56 && tileY >= 19 && tileY <= 20;
        }

        /// <summary>
        ///     Checks if the travelling cart is occupying the given tile coordinates in the given
        ///     game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if the travelling cart is occupying the given tile coordinates in the
        ///     given game location. Returns false otherwise.
        /// </returns>
        public static bool ContainsTravellingCart(this GameLocation gameLocation, int tileX, int tileY)
        {
            Rectangle tileRectangle = new Rectangle(
                tileX * Game1.tileSize,
                tileY * Game1.tileSize,
                Game1.tileSize,
                Game1.tileSize);

            return gameLocation is Forest { travelingMerchantBounds: { } } forest && forest.travelingMerchantBounds.Any(
                travelingMerchantBounds => travelingMerchantBounds.Intersects(tileRectangle));
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
            Rectangle tileRectangle = new Rectangle(
                tileX * Game1.tileSize,
                tileY * Game1.tileSize,
                Game1.tileSize,
                Game1.tileSize);

            return gameLocation is Desert desert && desert.IsTravelingMerchantHere()
                                                 && desert.GetDesertMerchantBounds().Intersects(tileRectangle);
        }

        public static float Distance(Point point1, Point point2)
        {
            float num1 = point1.X - point2.X;
            float num2 = point1.Y - point2.Y;
            return (float)Math.Sqrt((num1 * num1) + (num2 * num2));
        }

        public static Rectangle GetAlternativeBoundingBox(this Horse horse)
        {
            if (horse.FacingDirection == WalkDirection.Up.Value || horse.FacingDirection == WalkDirection.Down.Value)
            {
                return new Rectangle((int)horse.Position.X, (int)horse.Position.Y - 128, 64, 192);
            }

            return new Rectangle((int)horse.Position.X - 32, (int)horse.Position.Y, 128, 64);
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

            return gameLocation is Farm farm
                       ? farm.animals.Values.FirstOrDefault(farmAnimal => farmAnimal.GetBoundingBox().Contains(x, y))
                       : null;
        }

        public static NPC GetFestivalHost(this Event festival)
        {
            if (ClickToMoveHelper.FestivalNames.Contains(festival.FestivalName))
            {
                return festival.getActorByName("Lewis");
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

        public static Furniture GetFurnitureClickedOn(this GameLocation gameLocation, int clickPointX, int clickPointY)
        {
            return gameLocation is DecoratableLocation decoratableLocation
                       ? decoratableLocation.furniture.FirstOrDefault(
                           furniture => furniture.getBoundingBox(furniture.tileLocation.Value)
                               .Contains(clickPointX, clickPointY))
                       : null;
        }

        /// <summary>
        ///     Checks whether this <see cref="Farmer"/> has the given tool in their inventory.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <param name="toolName">The tool name.</param>
        /// <returns>
        ///     Returns true if this <see cref="Farmer"/> has the given tool in their inventory.
        ///     Return false otherwise.
        /// </returns>
        public static bool HasTool(this Farmer farmer, string toolName)
        {
            return farmer.items.Any(item => item?.Name.Contains(toolName) == true);
        }

        public static void Init(IMonitor monitor, IReflectionHelper reflection)
        {
            ClickToMoveHelper.monitor = monitor;
            ClickToMoveHelper.reflection = reflection;
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
        public static bool IsBoulderAt(this ResourceClump resourceClump, int x, int y)
        {
            return resourceClump.occupiesTile(x, y)
                   && (resourceClump.parentSheetIndex.Value == ResourceClump.meteoriteIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.boulderIndex
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock1Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock2Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock3Index
                       || resourceClump.parentSheetIndex.Value == ResourceClump.mineRock4Index);
        }

        /// <summary>
        ///     Checks whether a bush is destroyable from a given tile. Extends the game's <see
        ///     cref="Bush.isDestroyable"/> method to deal with bushes created by the Deep Woods mod.
        /// </summary>
        /// <param name="bush">The <see cref="Bush"/> instance.</param>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the bush is.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>Returns true if the bush can be destroyed by the player, false otherwise.</returns>
        public static bool IsDestroyable(this Bush bush, GameLocation gameLocation, int tileX, int tileY)
        {
            if (bush.isDestroyable(gameLocation, new Vector2(tileX, tileY)))
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
                return !ClickToMoveHelper.reflection.GetField<NetBool>(rockCrab, "shellGone").GetValue().Value;
            }

            return false;
        }

        public static bool IsMatureTreeStumpOrBoulderAt(this GameLocation gameLocation, Vector2 tile)
        {
            gameLocation.terrainFeatures.TryGetValue(tile, out TerrainFeature terrainFeature);

            int tileX = (int)tile.X;
            int tileY = (int)tile.Y;

            if (terrainFeature is Tree || terrainFeature is FruitTree
                                       || (terrainFeature is Bush bush
                                           && bush.IsDestroyable(gameLocation, tileX, tileY)))
            {
                return true;
            }

            foreach (LargeTerrainFeature largeTerrainFeature in gameLocation.largeTerrainFeatures)
            {
                if (largeTerrainFeature is Bush bush2
                    && bush2.getRenderBounds(new Vector2(bush2.tilePosition.X, bush2.tilePosition.Y)).Contains(
                        (int)(tile.X * Game1.tileSize),
                        (int)(tile.Y * Game1.tileSize)) && bush2.IsDestroyable(gameLocation, tileX, tileY))
                {
                    return true;
                }
            }

            return gameLocation.IsStumpAt(tileX, tileY)
                   || gameLocation.IsMinableAt(tileX, tileY);
        }

        public static bool IsMinableAt(this GameLocation gameLocation, int x, int y)
        {
            if (!(gameLocation is Forest || gameLocation is Woods) && gameLocation.resourceClumps.Any(
                    resourceClump => resourceClump.IsBoulderAt(x, y)))
            {
                return true;
            }

            gameLocation.objects.TryGetValue(new Vector2(x, y), out SObject @object);

            return @object is not null && (@object.Name == "Stone" || @object.Name == "Boulder");
        }

        public static bool IsOreAt(this GameLocation gameLocation, Vector2 tile)
        {
            return gameLocation.orePanPoint.Value != Point.Zero && gameLocation.orePanPoint.X == tile.X
                                                            && gameLocation.orePanPoint.Y == tile.Y;
        }

        /// <summary>
        ///     Checks if this player is passing out.
        /// </summary>
        /// <param name="farmer">The <see cref="Farmer"/> instance.</param>
        /// <returns>
        ///     Returns true if this player is passing out in the current tick. Returns false otherwise.
        /// </returns>
        public static bool IsPassingOut(this Farmer farmer)
        {
            return farmer.passedOut || farmer.FarmerSprite.isPassingOut();
        }

        /// <summary>
        ///     Checks if a fence is an isolated gate, i.e. a gate that has no neighbour fences.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the fence is.</param>
        /// <param name="fence">The <see cref="Fence"/> to check.</param>
        /// <returns>
        ///     Returns true if the given fence is a gate and has no other fences around it. Returns
        ///     false otherwise.
        /// </returns>
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

            if (gameLocation is not null and not MineShaft and not Forest)
            {
                return gameLocation.resourceClumps.Any(
                    resourceClump => resourceClump.occupiesTile(x, y)
                                     && (resourceClump.parentSheetIndex.Value == ResourceClump.stumpIndex
                                         || resourceClump.parentSheetIndex.Value == ResourceClump.hollowLogIndex));
            }

            return false;
        }

        public static bool IsTileHoeable(this GameLocation gameLocation, Vector2 tile)
        {
            if (gameLocation.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") is not null
                && !gameLocation.isTileOccupied(tile))
            {
                return gameLocation.isTilePassable(new Location((int)tile.X, (int)tile.Y), Game1.viewport);
            }

            return false;
        }

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> to check.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>Returns true if the given tile is passable. Returns false otherwise.</returns>
        public static bool IsTilePassable(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation.IsTilePassable(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize));
        }

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> to check.</param>
        /// <param name="location">The tile position in pixels.</param>
        /// <returns>Returns true if the given tile is passable. Returns false otherwise.</returns>
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
        ///     Returns true if the travelling merchant is present at the desert; returns false otherwise.
        /// </returns>
        public static bool IsTravelingMerchantHere(this Desert desert)
        {
            if (Game1.currentSeason == "winter" && Game1.dayOfMonth >= 15)
            {
                return Game1.dayOfMonth > 17;
            }

            return true;
        }

        public static bool IsWater(this GameLocation location, int tileX, int tileY)
        {
            if (location is Submarine && tileX >= 9 && tileX <= 20 && tileY >= 7 && tileY <= 11)
            {
                return true;
            }

            if (location.doesTileHaveProperty(tileX, tileY, "Water", "Back") is null)
            {
                return location.doesTileHaveProperty(tileX, tileY, "WaterSource", "Back") is not null;
            }

            return true;
        }

        public static bool IsWateringCanFillingSource(this GameLocation gameLocation, int tileX, int tileY)
        {
            if (gameLocation.IsWater(tileX, tileY) && !gameLocation.IsTilePassable(tileX, tileY))
            {
                return true;
            }

            if (gameLocation is BuildableGameLocation buildableGameLocation)
            {
                Building building = buildableGameLocation.getBuildingAt(new Vector2(tileX, tileY));
                if (building is not null && (building is FishPond || building.buildingType.Equals("Well"))
                                     && building.daysOfConstructionLeft.Value <= 0)
                {
                    return true;
                }
            }

            if (gameLocation.name.Value == "Greenhouse"
                && ((tileX == 9 && tileY == 7) || (tileX == 10 && tileY == 7)))
            {
                return true;
            }

            if (gameLocation.name.Value == "Railroad" && tileX >= 14 && tileX <= 16 && tileY >= 55 && tileY <= 56)
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
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="gameLocation"></param>
        /// <returns></returns>
        public static bool NpcAtWarpOrDoor(this NPC npc, GameLocation gameLocation)
        {
            if (gameLocation.isCollidingWithWarp(npc.GetBoundingBox(), npc) is not null)
            {
                return true;
            }

            PropertyValue action = null;
            gameLocation.map.GetLayer("Buildings").PickTile(npc.nextPositionPoint(), Game1.viewport.Size)?.Properties
                .TryGetValue("Action", out action);
            return action is not null;
        }

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

        public static bool ReadyToHarvest(this Crop crop)
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

        /// <summary> This <see cref="Farmer"/> selects a tool by name. </summary> <<param
        /// name="farmer">This <see cref="Farmer"/> instance.</param> <param name="toolName"> The
        /// tool name. </param> <returns> Returns true if the tool was found and selected in the
        /// player's inventory; false, otherwise. </returns>
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
        public static int SquaredEuclideanDistance(int x1, int y1, int x2, int y2)
        {
            int deltaX = x1 - x2;
            int deltaY = y1 - y2;
            return (deltaX * deltaX) + (deltaY * deltaY);
        }

        public static bool WarpIfInRange(this Farmer farmer, GameLocation gameLocation, Vector2 clickPoint)
        {
            float warpRange = gameLocation.WarpRange();

            foreach (Warp warp in gameLocation.warps)
            {
                Vector2 warpVector = new Vector2(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

                if (Vector2.Distance(warpVector, clickPoint) < warpRange
                    && Vector2.Distance(warpVector, Game1.player.OffsetPositionOnMap()) < warpRange)
                {
                    Game1.player.warpFarmer(warp);

                    return true;
                }
            }

            return false;
        }

        public static float WarpRange(this GameLocation gameLocation)
        {
            if (gameLocation is not null && (gameLocation.isOutdoors || gameLocation is BathHousePool))
            {
                return Game1.tileSize * 2;
            }

            return Game1.tileSize * 1.5f;
        }
    }
}
