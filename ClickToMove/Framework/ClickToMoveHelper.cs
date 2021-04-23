// ------------------------------------------------------------------------------------------------
// <copyright file="ClickToMoveHelper.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

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

    /// <summary>
    ///     Provides methods for interacting with the game code.
    /// </summary>
    internal static class ClickToMoveHelper
    {
        public static bool AtWarpOrDoor(this NPC npc, GameLocation gameLocation)
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
        ///     Checks if this <see cref="Wallpaper"/> can really be placed at the clicked tile in
        ///     the given <see cref="DecoratableLocation"/>.
        /// </summary>
        /// <param name="wallpaper">The <see cref="Wallpaper"/> instance.</param>
        /// <param name="decoratableLocation">
        ///     The <see cref="DecoratableLocation"/> where the Wallpaper is being placed.
        /// </param>
        /// <param name="tileX">The x coordinate of the tile clicked.</param>
        /// <param name="tileY">The y coordinate of the tile clicked.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this <see cref="Wallpaper"/> can really be placed
        ///     at the clicked tile in the given <see cref="DecoratableLocation"/>. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public static bool CanBePlaced(this Wallpaper wallpaper, DecoratableLocation decoratableLocation, int tileX, int tileY)
        {
            return wallpaper.isFloor.Value ? decoratableLocation.getFloorAt(new Point(tileX, tileY)) != -1 : decoratableLocation.isTileOnWall(tileX, tileY);
        }

        /// <summary>
        ///     Checks whether there's an egg at the given point in the world during the egg festival.
        /// </summary>
        /// <param name="clickPoint">The point to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a festival egg at the given point. Returns
        ///     <see langword="false"/> otherwise.
        /// </returns>
        public static bool ClickedEggAtEggFestival(Point clickPoint)
        {
            return Game1.CurrentEvent is not null
                && Game1.CurrentEvent.FestivalName == "Egg Festival"
                && Game1.CurrentEvent.festivalProps.Any(prop => prop.ContainsPoint(clickPoint.X, clickPoint.Y));
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

        /// <summary>
        ///     Checks if a point is within the bounding box of this <see cref="Prop"/>.
        /// </summary>
        /// <param name="prop">The <see cref="Prop"/> instance.</param>
        /// <param name="pointX">The x absolute coordinate of the point to check.</param>
        /// <param name="pointY">The y absolute coordinate of the point to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given point is within the bounding box of this
        ///     <see cref="Prop"/>. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool ContainsPoint(this Prop prop, int pointX, int pointY)
        {
            Rectangle boundingRect = ClickToMoveManager.Reflection.GetField<Rectangle>(prop, "boundingRect").GetValue();
            return boundingRect.Contains(pointX, pointY);
        }

        /// <summary>
        ///     Checks if the travelling cart is occupying the given tile coordinates in the given
        ///     game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the travelling cart is occupying the given tile
        ///     coordinates in the given game location. Returns <see langword="false"/> otherwise.
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
        ///     Returns <see langword="true"/> if the travelling desert shop is occupying the given tile coordinates
        ///     in the given game location. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool ContainsTravellingDesertShop(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation is Desert desert && desert.IsTravelingMerchantHere() && desert.GetDesertMerchantBounds().Contains(tileX, tileY);
        }

        /// <summary>
        ///     Gets the Euclidean distance between two points.
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <returns>Returns the Euclidean distance between the two given points.</returns>
        public static float Distance(Point point1, Point point2)
        {
            double num1 = point1.X - point2.X;
            double num2 = point1.Y - point2.Y;
            return (float)Math.Sqrt((num1 * num1) + (num2 * num2));
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
        ///     Gets the farm animal at the given position in ths <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="x">The x absolute coordinate.</param>
        /// <param name="y">The y absolute coordinate.</param>
        /// <returns>
        ///     Returns the farm animal at the given position in ths <see cref="GameLocation"/>, if
        ///     there's one. Returns <see langword="null"/> if there isn't any animal at the
        ///     specified position.
        /// </returns>
        public static FarmAnimal GetFarmAnimal(this GameLocation gameLocation, int x, int y)
        {
            Rectangle rectangle = new Rectangle(x, y, Game1.tileSize, Game1.tileSize);

            if (gameLocation is AnimalHouse animalHouse)
            {
                foreach (FarmAnimal farmAnimal in animalHouse.animals.Values)
                {
                    if (farmAnimal.GetBoundingBox().Intersects(rectangle))
                    {
                        return farmAnimal;
                    }
                }
            }
            else if (gameLocation is Farm farm)
            {
                foreach (FarmAnimal farmAnimal in farm.animals.Values)
                {
                    if (farmAnimal.GetBoundingBox().Intersects(rectangle))
                    {
                        return farmAnimal;
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Gets the number of tiles added to the maximum possible fishing casting distance,
        ///     depending on the <see cref="Farmer"/>'s skill level.
        /// </summary>
        /// <param name="who">The <see cref="Farmer"/> fishing.</param>
        /// <returns>
        ///     The number of tiles added to the maximum possible fishing casting distance according
        ///     to the <see cref="Farmer"/>'s skill level.
        /// </returns>
        public static int GetFishingAddedDistance(this Farmer who)
        {
            return who.FishingLevel switch
            {
                >= 15 => 4,
                >= 8 => 3,
                >= 4 => 2,
                >= 1 => 1,
                _ => 0
            };
        }

        /// <summary>
        ///     Gets the piece of furniture occupying a given position in this <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="x">The x absolute coordinate of the position to check.</param>
        /// <param name="y">The y absolute coordinate of the position to check.</param>
        /// <returns>
        ///     Returns the piece of furniture occupying the given position in this <see
        ///     cref="GameLocation"/>, if any. Returns <see langword="null"/> otherwise.
        /// </returns>
        public static Furniture GetFurniture(this GameLocation gameLocation, int x, int y)
        {
            return gameLocation.furniture.FirstOrDefault(
                furniture => furniture.getBoundingBox(furniture.tileLocation.Value).Contains(x, y));
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
                return Game1.currentMinigame
                    is AbigailGame
                    or FantasyBoardGame
                    or GrandpaStory
                    or HaleyCowPictures
                    or MineCart
                    or PlaneFlyBy
                    or RobotBlastoff
                    or Slots
                    or TargetGame;
            }

            return false;
        }

        /// <summary>
        ///     Checks if there's a boulder at a tile in a game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a boulder at the given tile in this game
        ///     location. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsBoulderAt(this GameLocation gameLocation, int tileX, int tileY)
        {
            if (gameLocation is not Forest
                && gameLocation is not Woods
                && gameLocation.resourceClumps.Any(resourceClump => resourceClump.IsBoulderAt(tileX, tileY)))
            {
                return true;
            }

            gameLocation.objects.TryGetValue(new Vector2(tileX, tileY), out SObject @object);

            return @object is not null && (@object.Name is "Stone" or "Boulder");
        }

        /// <summary>
        ///     Checks if a resource clump is a boulder occupying a given tile.
        /// </summary>
        /// <param name="resourceClump">The <see cref="ResourceClump"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this <see cref="ResourceClump"/> is a boulder at the given tile
        ///     coordinates. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsBoulderAt(this ResourceClump resourceClump, int tileX, int tileY)
        {
            return resourceClump.occupiesTile(tileX, tileY)
                   && resourceClump.parentSheetIndex.Value is ResourceClump.meteoriteIndex
                       or ResourceClump.boulderIndex
                       or ResourceClump.mineRock1Index
                       or ResourceClump.mineRock2Index
                       or ResourceClump.mineRock3Index
                       or ResourceClump.mineRock4Index;
        }

        /// <summary>
        ///     Checks if there's something that can be chopped or mined at a tile in this <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tile">The tile to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's something that can be chopped or mined at
        ///     the given tile in this <see cref="GameLocation"/>. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsChoppableOrMinable(this GameLocation gameLocation, Point tile)
        {
            gameLocation.terrainFeatures.TryGetValue(new Vector2(tile.X, tile.Y), out TerrainFeature terrainFeature);

            if (terrainFeature is Tree
                || terrainFeature is FruitTree
                || (terrainFeature is Bush bush && bush.IsDestroyable(gameLocation, tile)))
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

            return gameLocation.IsStumpOrBoulderAt(tile.X, tile.Y);
        }

        /// <summary>
        ///     Checks whether a bush is destroyable from a given tile. Extends the game's <see
        ///     cref="Bush.isDestroyable"/> method to deal with bushes created by the Deep Woods mod.
        /// </summary>
        /// <param name="bush">The <see cref="Bush"/> instance.</param>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the bush is.</param>
        /// <param name="tile">The tile to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the bush can be destroyed by the player, <see
        ///     langword="false"/> otherwise.
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

        /// <summary>
        ///     Checks if this <see cref="RockCrab"/> is hiding in its shell.
        /// </summary>
        /// <param name="rockCrab">The <see cref="RockCrab"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this <see cref="RockCrab"/> is hiding in its
        ///     shell. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsHidingInShell(this RockCrab rockCrab)
        {
            if (rockCrab.Sprite.currentFrame % 4 == 0)
            {
                return !ClickToMoveManager.Reflection.GetField<NetBool>(rockCrab, "shellGone").GetValue().Value;
            }

            return false;
        }

        public static bool IsOreAt(this GameLocation location, Point tile)
        {
            return location.orePanPoint.Value != Point.Zero
                && location.orePanPoint.X == tile.X
                && location.orePanPoint.Y == tile.Y;
        }

        /// <summary>
        ///     Checks if a fence is an isolated gate, i.e. a gate that has no neighbour fences.
        ///     Such fences can't be open and therefore are impassable.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> where the fence is.</param>
        /// <param name="fence">The <see cref="Fence"/> to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given fence is a gate and has no other fences
        ///     around it. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsSoloGate(this GameLocation gameLocation, Fence fence)
        {
            foreach (WalkDirection walkDirection in WalkDirection.CardinalDirections)
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
        ///     Checks if there's a tree stump or a boulder at a tile in a game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a tree stump or a boulder at the given
        ///     tile in this game location. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsStumpOrBoulderAt(this GameLocation gameLocation, int tileX, int tileY)
        {
            switch (gameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(stump => stump.occupiesTile(tileX, tileY)))
                    {
                        return true;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(tileX, tileY))
                    {
                        return true;
                    }

                    break;
                default:
                    if (gameLocation.resourceClumps.Any(resourceClump => resourceClump.occupiesTile(tileX, tileY)))
                    {
                        return true;
                    }

                    break;
            }

            gameLocation.objects.TryGetValue(new Vector2(tileX, tileY), out SObject @object);

            return @object is not null && (@object.Name is "Stone" or "Boulder");
        }

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> to check.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile is passable. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public static bool IsTilePassable(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation.IsTilePassable(new Location(tileX * Game1.tileSize, tileY * Game1.tileSize));
        }

        /// <summary>
        ///     Checks if a tile is passable, i.e. it can be reached by the player.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> to check.</param>
        /// <param name="location">The tile position in pixels.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile is passable. Returns <see
        ///     langword="false"/> otherwise.
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

        /// <summary>
        ///     Checks if there's a tree log at a tile in a game location.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a tree log at the given tile in this game
        ///     location. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool IsTreeLogAt(this GameLocation gameLocation, int tileX, int tileY)
        {
            return gameLocation is Forest forest
                && forest.log is not null
                && forest.log.occupiesTile(tileX, tileY);
        }

        public static bool IsWizardBuilding(this GameLocation gameLocation, Vector2 tile)
        {
            if (gameLocation is BuildableGameLocation buildableGameLocation)
            {
                Building buildingAt = buildableGameLocation.getBuildingAt(tile);

                if (buildingAt is not null
                    && (buildingAt.buildingType.Contains("Obelisk")
                        || buildingAt.buildingType == "Junimo Hut"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if this bed effectively occupies a given position.
        /// </summary>
        /// <param name="bed">The <see cref="BedFurniture"/> instance.</param>
        /// <param name="x">The x absolute coordinate to check.</param>
        /// <param name="y">The y absolute coordinate to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if this bed effectively occupies a given position,
        ///     i.e. the given position is within the limits of the bed's bounding box and can not
        ///     be walked on. Returns <see langword="false"/> otherwise.
        /// </returns>
        public static bool Occupies(this BedFurniture bed, int x, int y)
        {
            Rectangle bounds = bed.getBoundingBox(bed.TileLocation);

            Rectangle rectangle = bounds;
            rectangle.Height = Game1.tileSize;
            if (rectangle.Contains(x, y))
            {
                return true;
            }

            rectangle = bounds;
            rectangle.Y += 2 * Game1.tileSize;
            rectangle.Height -= 2 * Game1.tileSize;
            if (rectangle.Contains(x, y))
            {
                return true;
            }

            return false;
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
            return (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0)
                && crop.currentPhase.Value >= crop.phaseDays.Count - 1
                && !crop.dead
                && (!crop.forageCrop.Value || crop.whichForageCrop.Value != Crop.forageCrop_ginger);
        }

        /// <summary>
        ///     This <see cref="Farmer"/> equips a heavy hitter <see cref="Tool"/> from their inventory.
        /// </summary>
        /// <param name="farmer">This <see cref="Farmer"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if an heavy hitter tool was found and selected in the
        ///     farmer's inventory; <see langword="false"/>, otherwise.
        /// </returns>
        public static bool SelectHeavyHitter(this Farmer farmer)
        {
            for (int i = 0; i < farmer.items.Count; i++)
            {
                if (farmer.items[i] is not null && farmer.items[i] is Tool tool && tool.isHeavyHitter())
                {
                    farmer.CurrentToolIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     This <see cref="Farmer"/> equips a <see cref="MeleeWeapon"/> from their inventory.
        /// </summary>
        /// <remarks>
        ///     The scythe is <see cref="MeleeWeapon"/> in the game but is ignored by this method.
        /// </remarks>
        /// <param name="farmer">This <see cref="Farmer"/> instance.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if a melee weapon was found and selected in the
        ///     farmer's inventory; <see langword="false"/>, otherwise.
        /// </returns>
        public static bool SelectMeleeWeapon(this Farmer farmer)
        {
            for (int i = 0; i < farmer.items.Count; i++)
            {
                if (farmer.items[i] is not null && farmer.items[i] is MeleeWeapon meleeWeapon && !meleeWeapon.isScythe())
                {
                    farmer.CurrentToolIndex = i;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     This <see cref="Farmer"/> selects a tool by name.
        /// </summary>
        /// <param name="farmer">This <see cref="Farmer"/> instance.</param>
        /// <param name="toolName">The tool's name.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the tool was found and selected in the farmer's
        ///     inventory; <see langword="false"/>, otherwise.
        /// </returns>
        public static bool SelectTool(this Farmer farmer, string toolName)
        {
            if (farmer.CurrentTool is Tool tool && tool.Name.Contains(toolName))
            {
                return true;
            }

            for (int i = 0; i < farmer.items.Count; i++)
            {
                if (farmer.items[i] is not null && farmer.items[i] is Tool && farmer.items[i].Name.Contains(toolName))
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
        ///     Returns <see langword="true"/> if the player is successfully warped. return <see
        ///     langword="false"/> otherwise.
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
        ///     Warps the player through a <see cref="Warp"/> that's in range of both the player and
        ///     the clicked point, if such a warp exists.
        /// </summary>
        /// <param name="gameLocation">The current game location.</param>
        /// <param name="clickPoint">The point clicked.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the player is successfully warped. return <see
        ///     langword="false"/> otherwise.
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
    }
}
