// -----------------------------------------------------------------------
// <copyright file="AStarNode.cs" company="Raquellcesar">
//      Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//      Use of this source code is governed by an MIT-style license that can be
//      found in the LICENSE file in the project root or at
//      https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework.PathFinding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.Common;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Locations;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;

    using xTile.Dimensions;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     The class for nodes used by the <see cref="AStarGraph" /> class.
    /// </summary>
    public class AStarNode : IComparable<AStarNode>
    {
        /// <summary>
        ///     The graph to which the node belongs.
        /// </summary>
        private readonly AStarGraph graph;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarNode" /> class.
        /// </summary>
        /// <param name="graph">
        ///     The <see cref="AStarGraph" /> to which this node belongs.
        /// </param>
        /// <param name="x">
        ///     The x tile coordinate.
        /// </param>
        /// <param name="y">
        ///     The y tile coordinate.
        /// </param>
        public AStarNode(AStarGraph graph, int x, int y)
        {
            this.graph = graph;

            this.X = x;
            this.Y = y;

            this.GCost = int.MaxValue;
        }

        public Rectangle BoundingBox =>
            new Rectangle(this.X * Game1.tileSize, this.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

        public bool BubbleChecked { get; set; }

        public int BubbleId { get; set; } = -1;

        public int BubbleId2 { get; set; } = -1;

        public bool FakeTileClear { get; set; }

        /// <summary>
        ///     Gets the estimated total cost of the cheapest path from the start node to the goal that goes through this node.
        /// </summary>
        public int FCost => this.GCost + this.HCost;

        /// <summary>
        ///     Gets or sets the cost of the path from the start node to this node.
        /// </summary>
        public int GCost { get; set; }

        /// <summary>
        ///     Gets or sets the estimated cost of the cheapest path from this node to the goal.
        /// </summary>
        public int HCost { get; set; }

        public Vector2 NodeCenterOnMap =>
            new Vector2(
                (this.X * Game1.tileSize) + (Game1.tileSize / 2),
                (this.Y * Game1.tileSize) + (Game1.tileSize / 2));

        /// <summary>
        ///     Gets or sets the previous node in the path.
        /// </summary>
        public AStarNode PreviousNode { get; set; }

        public bool TileClear
        {
            get
            {
                if (this.FakeTileClear)
                {
                    return true;
                }

                return this.graph.IsTileOnMap(this.X, this.Y) && this.IsTilePassableAndUnoccupied();
            }
        }

        /// <summary>
        ///     Gets the rectangle surrounding the tile represented by this node.
        /// </summary>
        public Rectangle TileRectangle =>
            new Rectangle(this.X * Game1.tileSize, this.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

        /// <summary>
        ///     Gets or sets the x tile coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        ///     Gets or sets the y tile coordinate.
        /// </summary>
        public int Y { get; set; }

        public bool BrokenFestivalTile()
        {
            if (Game1.CurrentEvent is not null)
            {
                if (this.X == 18 && this.Y == 31 && Game1.dayOfMonth == 16 && Game1.currentSeason == "fall")
                {
                    return true;
                }

                if (this.X == 16 && this.Y == 19 && Game1.dayOfMonth == 27 && Game1.currentSeason == "fall")
                {
                    return true;
                }

                if (this.X == 66 && this.Y == 4 && Game1.dayOfMonth == 8 && Game1.currentSeason == "winter")
                {
                    return true;
                }

                if (this.X == 103 && this.Y == 28 && Game1.dayOfMonth == 8 && Game1.currentSeason == "winter")
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        /// <remarks>The comparison between AStarNodes is made by comparing their <see cref="FCost" /> values.</remarks>
        public int CompareTo(AStarNode other)
        {
            return this.FCost.CompareTo(other.FCost);
        }

        /// <summary>
        ///     Checks whether there's an animal at the tile represented by this node.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if there's an animal here. Returns <see langword="false"/> otherwise.</returns>
        public bool ContainsAnimal()
        {
            return this.graph.GameLocation switch
                {
                    AnimalHouse animalHouse => animalHouse.animals.Values.Any(
                        animal => animal.getTileX() == this.X && animal.getTileY() == this.Y),
                    Farm farm => farm.animals.Values.Any(
                        animal => animal.getTileX() == this.X && animal.getTileY() == this.Y),
                    _ => false
                };
        }

        public bool ContainsBoulder()
        {
            return this.graph.GameLocation.IsBoulderAt(this.X, this.Y);
        }

        public bool ContainsBuilding()
        {
            Vector2 position = new Vector2(this.X, this.Y);

            if (this.graph.GameLocation is BuildableGameLocation buildableGameLocation)
            {
                foreach (Building building in buildableGameLocation.buildings)
                {
                    if (!building.isTilePassable(position))
                    {
                        return true;
                    }
                }
            }

            return this.graph.GameLocation.map.GetLayer("Buildings").PickTile(
                       new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize),
                       Game1.viewport.Size) is not null;
        }

        /// <summary>
        ///     Checks whether this node represents a tile in the Cinema.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if this node's tile is a Cinema's tile. Returns <see langword="false"/> otherwise.</returns>
        public bool ContainsCinema()
        {
            if (this.graph.GameLocation is Town
                && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
            {
                switch (this.X)
                {
                    case >= 47 and <= 58 when this.Y >= 17 && this.Y <= 19:
                    case 47 when this.Y == 20:
                    case >= 55 and <= 58 when this.Y == 20:
                        return true;
                }
            }

            return false;
        }

        public bool ContainsCinemaDoor()
        {
            return this.graph.GameLocation is Town
                   && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater")
                   && (this.X == 52 || this.X == 53) && (this.Y == 18 || this.Y == 19);
        }

        public bool ContainsCinemaTicketOffice()
        {
            return this.graph.GameLocation is Town
                   && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater") && this.X >= 54
                   && this.X <= 56 && (this.Y == 19 || this.Y == 20);
        }

        public bool ContainsFence()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);
            return @object is Fence;
        }

        /// <summary>
        ///     Checks whether there's a festival prop here.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if this node's tile contains a a festival prop. Returns <see langword="false"/> otherwise.</returns>
        public bool ContainsFestivalProp()
        {
            if (Game1.CurrentEvent is not null)
            {
                foreach (Prop prop in Game1.CurrentEvent.festivalProps)
                {
                    if (prop.isColliding(this.TileRectangle))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if there is any piece of furniture occupying the tile that this node represents.
        ///     Ignores beds.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if there is some furniture occupying this node's tile.</returns>
        public bool ContainsFurniture()
        {
            return this.graph.GameLocation is DecoratableLocation decoratableLocation
                   && decoratableLocation.furniture.Any(
                       furniture => furniture is not BedFurniture && furniture
                                        .getBoundingBox(furniture.tileLocation.Value).Intersects(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if there is any piece of furniture occupying the tile that this node represents.
        ///     Ignores beds and rugs.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if there is some furniture occupying this node's tile.</returns>
        public bool ContainsFurnitureIgnoreRugs()
        {
            return this.graph.GameLocation is DecoratableLocation decoratableLocation
                   && decoratableLocation.furniture.Any(
                       furniture => furniture is not BedFurniture
                                    && furniture.furniture_type.Value != (int)FurnitureType.Rug
                                    && furniture.getBoundingBox(furniture.tileLocation.Value)
                                        .Intersects(this.TileRectangle));
        }

        public bool ContainsGate()
        {
            Vector2 position = new Vector2(this.X, this.Y);

            if (this.graph.GameLocation.objects.ContainsKey(position))
            {
                SObject @object = this.graph.GameLocation.objects[position];
                if (@object is Fence fence && fence.isGate.Value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if there is an NPC occupying the tile that this node represents.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if there is some NPC occupying this node's tile.</returns>
        public bool ContainsNpc()
        {
            if (this.graph.GameLocation is Beach && this.graph.OldMariner is not null
                                                 && this.graph.OldMariner.getTileX() == this.X
                                                 && this.graph.OldMariner.getTileY() == this.Y)
            {
                return true;
            }

            foreach (NPC npc in this.graph.GameLocation.characters)
            {
                if ((npc is not Pet pet || !pet.isSleepingOnFarmerBed) && npc.getTileX() == this.X
                                                                       && npc.getTileY() == this.Y)
                {
                    return true;
                }
            }

            return this.graph.GameLocation.currentEvent?.actors?.Any(
                       npc => npc.getTileX() == this.X && npc.getTileY() == this.Y) == true;
        }

        public bool ContainsScarecrow()
        {
            Vector2 key = new Vector2(this.X, this.Y);

            if (this.graph.GameLocation.objects.TryGetValue(key, out SObject @object))
            {
                if (@object.parentSheetIndex.Value == 8 || @object.parentSheetIndex.Value == 167
                                                        || @object.parentSheetIndex.Value == 110
                                                        || @object.parentSheetIndex.Value == 113
                                                        || @object.parentSheetIndex.Value == 126
                                                        || @object.parentSheetIndex.Value == 136
                                                        || @object.parentSheetIndex.Value == 137
                                                        || @object.parentSheetIndex.Value == 138
                                                        || @object.parentSheetIndex.Value == 139
                                                        || @object.parentSheetIndex.Value == 140)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsSomeKindOfWarp()
        {
            Tile tile = this.graph.GameLocation.map.GetLayer("Buildings").PickTile(
                new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize),
                Game1.viewport.Size);

            return tile is not null && tile.Properties.Select(property => (string)property.Value).Any(
                       text => text.Contains("LockedDoorWarp") || text.Contains("Warp")
                                                               || text.Contains("WarpMensLocker")
                                                               || text.Contains("WarpWomensLocker"));
        }

        /// <summary>
        ///     Checks for the existence of a stump at the world location represented by this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a stump at the location represented by this node.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsStump()
        {
            return this.graph.GameLocation.IsStumpAt(this.X, this.Y);
        }

        /// <summary>
        ///     Checks for the existence of a stump or boulder at the world location represented by this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a stump or boulder at the location represented by this node.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsStumpOrBoulder()
        {
            switch (this.graph.GameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return true;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(this.X, this.Y))
                    {
                        return true;
                    }

                    break;
                default:
                    if (this.graph.GameLocation.resourceClumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return true;
                    }

                    break;
            }

            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject value);

            return value is not null && value.Name == "Boulder";
        }

        public bool ContainsStumpOrHollowLog()
        {
            switch (this.graph.GameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return true;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(this.X, this.Y))
                    {
                        return true;
                    }

                    break;
                default:
                    if (this.graph.GameLocation.resourceClumps.Any(
                            resourceClump => resourceClump.occupiesTile(this.X, this.Y)
                                             && (resourceClump.parentSheetIndex.Value == ResourceClump.hollowLogIndex
                                                 || resourceClump.parentSheetIndex.Value == ResourceClump.stumpIndex)))
                    {
                        return true;
                    }

                    break;
            }

            return false;
        }

        /// <summary>
        ///     Checks if the travelling cart is occupying the tile that this node represents.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if the travelling cart is occupying this node's tile.</returns>
        public bool ContainsTravellingCart()
        {
            return this.graph.GameLocation is Forest { travelingMerchantBounds: { } } forest
                   && forest.travelingMerchantBounds.Any(
                       travelingMerchantBounds => travelingMerchantBounds.Intersects(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if the travelling desert shop is occupying the tile that this node represents.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if the travelling desert shop is occupying this node's tile.</returns>
        public bool ContainsTravellingDesertShop()
        {
            return this.graph.GameLocation is Desert desert && desert.IsTravelingMerchantHere()
                                                            && desert.GetDesertMerchantBounds().Intersects(
                                                                this.TileRectangle);
        }

        public bool ContainsTree()
        {
            this.graph.GameLocation.terrainFeatures.TryGetValue(
                new Vector2(this.X, this.Y),
                out TerrainFeature terrainFeature);

            return terrainFeature is Tree || terrainFeature is FruitTree;
        }

        public AStarNode CrabPotNeighbour()
        {
            List<AStarNode> neighbours = this.GetNeighbours(true, false);

            foreach (AStarNode neighbour in neighbours)
            {
                SObject @object = neighbour.GetObject();
                if (@object is not null && @object.parentSheetIndex.Value == ObjectId.CrabPot)
                {
                    return neighbour;
                }
            }

            return null;
        }

        public Building GetBuilding()
        {
            Vector2 position = new Vector2(this.X, this.Y);
            if (this.graph.GameLocation is BuildableGameLocation buildableGameLocation)
            {
                foreach (Building building in buildableGameLocation.buildings)
                {
                    if (!building.isTilePassable(position))
                    {
                        return building;
                    }
                }
            }

            return null;
        }

        public Bush GetBush()
        {
            Vector2 key = new Vector2(this.X, this.Y);

            this.graph.GameLocation.terrainFeatures.TryGetValue(key, out TerrainFeature terrainFeature);
            if (terrainFeature is Bush bush)
            {
                return bush;
            }

            foreach (LargeTerrainFeature largeTerrainFeature in this.graph.GameLocation.largeTerrainFeatures)
            {
                if (largeTerrainFeature is Bush bush2 && bush2
                        .getRenderBounds(new Vector2(bush2.tilePosition.X, bush2.tilePosition.Y)).Contains(
                            this.X * Game1.tileSize,
                            this.Y * Game1.tileSize))
                {
                    return bush2;
                }
            }

            return null;
        }

        public Chest GetChest()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Chest chest ? chest : null;
        }

        public CrabPot GetCrabPot()
        {
            SObject @object = this.GetObject();

            if (@object is not null && @object.parentSheetIndex.Value == ObjectId.CrabPot)
            {
                return @object as CrabPot;
            }

            AStarNode node = this.graph.GetNode(this.X, this.Y + 1);
            if (node is not null)
            {
                @object = node.GetObject();
                if (@object?.parentSheetIndex.Value == ObjectId.CrabPot)
                {
                    CrabPot crabPot = (CrabPot)@object;
                    if (crabPot.readyForHarvest.Value)
                    {
                        return crabPot;
                    }
                }
            }

            node = this.graph.GetNode(this.X, this.Y + 2);
            if (node is not null)
            {
                @object = node.GetObject();
                if (@object?.parentSheetIndex.Value == ObjectId.CrabPot)
                {
                    CrabPot crabPot = (CrabPot)@object;
                    if (crabPot.readyForHarvest.Value)
                    {
                        return crabPot;
                    }
                }
            }

            return null;
        }

        public Furniture GetFurnitureIgnoreRugs()
        {
            return this.graph.GameLocation is DecoratableLocation decoratableLocation
                       ? decoratableLocation.furniture.FirstOrDefault(
                           furniture =>
                               furniture is not BedFurniture && furniture.furniture_type.Value != (int)FurnitureType.Rug
                                                             && furniture.getBoundingBox(furniture.tileLocation.Value)
                                                                 .Intersects(this.TileRectangle))
                       : null;
        }

        public Fence GetGate()
        {
            Vector2 key = new Vector2(this.X, this.Y);

            if (this.graph.GameLocation.objects.TryGetValue(key, out SObject @object))
            {
                if (@object is Fence fence && fence.isGate.Value)
                {
                    return fence;
                }
            }

            return null;
        }

        public GiantCrop GetGiantCrop()
        {
            if (this.graph.GameLocation is Farm farm)
            {
                foreach (ResourceClump resourceClump in farm.resourceClumps)
                {
                    if (resourceClump.occupiesTile(this.X, this.Y) && resourceClump is GiantCrop giantCrop)
                    {
                        return giantCrop;
                    }
                }
            }

            return null;
        }

        public AStarNode GetNearestNodeToCrabPot()
        {
            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                Point point = new Point(this.X + walkDirection.X, this.Y + walkDirection.Y);

                if (!this.graph.GameLocation.isWaterTile(point.X, point.Y))
                {
                    return this.graph.GetNode(point.X, point.Y);
                }
            }

            return this;
        }

        public AStarNode GetNeighbour(WalkDirection walkDirection, bool canWalkOnTile = true)
        {
            if (walkDirection != WalkDirection.None)
            {
                AStarNode neighbour = this.graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                if (neighbour is not null && neighbour.TileClear == canWalkOnTile)
                {
                    return neighbour;
                }
            }

            return null;
        }

        public AStarNode GetNeighbourPassable()
        {
            foreach (WalkDirection walkDirection in WalkDirection.SimpleDirections)
            {
                AStarNode neighbour = this.graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);

                if (neighbour is not null && neighbour.IsTilePassable() && neighbour.TileClear)
                {
                    return neighbour;
                }
            }

            return null;
        }

        /// <summary>
        ///     Gets a list of this node's neighbouring nodes.
        /// </summary>
        /// <param name="includeDiagonals">If diagonal directions should be considered. false by default.</param>
        /// <param name="canWalkOnTile">If true, only nodes that can be walked on will be considered. true by default.</param>
        /// <returns>Returns a list of nodes.</returns>
        public List<AStarNode> GetNeighbours(bool includeDiagonals = false, bool canWalkOnTile = true)
        {
            if (includeDiagonals)
            {
                return this.GetNeighboursInternal(WalkDirection.Directions, canWalkOnTile);
            }

            return this.GetNeighboursInternal(WalkDirection.SimpleDirections, canWalkOnTile);
        }

        public NPC GetNpc()
        {
            if (this.graph.GameLocation is Beach && this.graph.OldMariner is not null
                                                 && this.graph.OldMariner.getTileX() == this.X
                                                 && this.graph.OldMariner.getTileY() == this.Y)
            {
                return this.graph.OldMariner;
            }

            foreach (NPC npc in this.graph.GameLocation.characters)
            {
                if (npc.getTileX() == this.X && npc.getTileY() == this.Y)
                {
                    return npc;
                }
            }

            return this.graph.GameLocation.currentEvent?.actors?.FirstOrDefault(
                npc => npc.getTileX() == this.X && npc.getTileY() == this.Y);
        }

        public SObject GetObject()
        {
            Vector2 position = new Vector2(this.X, this.Y);
            return this.graph.GameLocation.objects.ContainsKey(position)
                       ? this.graph.GameLocation.objects[position]
                       : null;
        }

        public int GetObjectParentSheetIndex()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object?.parentSheetIndex.Value ?? -1;
        }

        public TerrainFeature GetTree()
        {
            this.graph.GameLocation.terrainFeatures.TryGetValue(
                new Vector2(this.X, this.Y),
                out TerrainFeature terrainFeature);

            if (terrainFeature is Tree tree)
            {
                return tree;
            }

            if (terrainFeature is FruitTree fruitTree)
            {
                return fruitTree;
            }

            return null;
        }

        public Warp GetWarp(bool ignoreWarps)
        {
            if (ignoreWarps)
            {
                return null;
            }

            Point nodeCenter = new Point(
                (this.X * Game1.tileSize) + (Game1.tileSize / 2),
                (this.Y * Game1.tileSize) + (Game1.tileSize / 2));
            float warpRange = this.graph.GameLocation.WarpRange();
            foreach (Warp warp in this.graph.GameLocation.warps)
            {
                Point warpPoint = new Point(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

                if (ClickToMoveHelper.Distance(warpPoint, nodeCenter) < warpRange)
                {
                    return warp;
                }
            }

            return null;
        }

        public bool IsBlockingBedTile()
        {
            if (this.graph.GameLocation is FarmHouse farmHouse)
            {
                Point bedSpot = farmHouse.getBedSpot();

                if (farmHouse.upgradeLevel == 0)
                {
                    if (this.Y == bedSpot.Y - 1 && (this.X == bedSpot.X || this.X == bedSpot.X - 1))
                    {
                        return true;
                    }
                }
                else if (farmHouse.upgradeLevel is >= 1)
                {
                    if (this.Y == bedSpot.Y + 2
                        && (this.X == bedSpot.X - 1 || this.X == bedSpot.X || this.X == bedSpot.X + 1))
                    {
                        return true;
                    }
                }
                else
                {
                    if (this.Y == bedSpot.Y + 2
                        && (this.X == bedSpot.X - 1 || this.X == bedSpot.X || this.X == bedSpot.X + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsDownLeft(AStarNode node)
        {
            return node is not null && this.X == node.X - 1 && this.Y == node.Y + 1;
        }

        public bool IsDownRightTo(AStarNode node)
        {
            return node is not null && this.X == node.X + 1 && this.Y == node.Y + 1;
        }

        public bool IsDownTo(AStarNode node)
        {
            return node is not null && this.X == node.X && this.Y == node.Y + 1;
        }

        public bool IsGate()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Fence fence && fence.isGate.Value && !this.graph.GameLocation.IsSoloGate(fence);
        }

        public bool IsLeftTo(AStarNode node)
        {
            return node is not null && this.X == node.X - 1 && this.Y == node.Y;
        }

        public bool IsNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return node.X >= this.X - 1 && node.X <= this.X + 1 && node.Y >= this.Y - 1 && node.Y <= this.Y + 1
                   && !this.IsSameNode(node);
        }

        /// <summary>
        ///     Checks if a node is a neighbour of this node in a specified direction.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <param name="walkDirection">The direction to check.</param>
        /// <returns>Returns <see langword="true"/> if the given node is adjacent to this node in the given direction.</returns>
        public bool IsNeighbourInDirection(AStarNode node, WalkDirection walkDirection)
        {
            return node is not null && walkDirection != WalkDirection.None && node.X == this.X + walkDirection.X
                   && node.Y == this.Y + walkDirection.Y;
        }

        public bool IsVerticalOrHorizontalNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return (node.X == this.X && (node.Y == this.Y + 1 || node.Y == this.Y - 1))
                   || (node.Y == this.Y && (node.X == this.X + 1 || node.X == this.X - 1));
        }

        public bool IsDiagonalNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return (node.X == this.X - 1 || node.X == this.X + 1) && (node.Y == this.Y - 1 || node.Y == this.Y + 1);
        }

        public bool IsRightTo(AStarNode node)
        {
            return node is not null && this.X == node.X + 1 && this.Y == node.Y;
        }

        public bool IsSameNode(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return node.X == this.X && node.Y == this.Y;
        }

        public bool IsTilePassable()
        {
            return this.graph.GameLocation.IsTilePassable(this.X, this.Y);
        }

        /// <summary>
        ///     Checks if a tile is passable and not occupied by some obstacle.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if the given tile position is passable and free of obstacles in the given game location.</returns>
        public bool IsTilePassableAndUnoccupied()
        {
            Vector2 tileVector = new Vector2(this.X, this.Y);

            Location tileLocation = new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize);

            if (!this.graph.GameLocation.IsTilePassable(tileLocation))
            {
                return false;
            }

            // Is there any object at the tile?
            this.graph.GameLocation.objects.TryGetValue(tileVector, out SObject @object);

            if (@object is not null)
            {
                return @object.isPassable() /* || @object is Fence fence && fence.isGate.Value && !this.graph.GameLocation.IsSoloGate(fence)*/;
            }

            // The tile interior.
            Rectangle rectangle = new Rectangle(
                tileLocation.X + 1,
                tileLocation.Y + 1,
                Game1.tileSize - 2,
                Game1.tileSize - 2);

            // Check for NPCs, ignoring horse.
            foreach (NPC npc in this.graph.GameLocation.characters)
            {
                if (!(npc is Pet pet && pet.isSleepingOnFarmerBed)
                    && npc?.GetBoundingBox().Intersects(rectangle) == true)
                {
                    return npc is Horse && Game1.player.isRidingHorse();
                }
            }

            // Check for terrain features.
            this.graph.GameLocation.terrainFeatures.TryGetValue(tileVector, out TerrainFeature terrainFeature);
            if (terrainFeature?.isPassable() == false
                && rectangle.Intersects(terrainFeature.getBoundingBox(tileVector)))
            {
                return false;
            }

            // Check for large terrain features.
            if (this.graph.GameLocation.largeTerrainFeatures?.Any(
                    largeTerrainFeature => largeTerrainFeature.getBoundingBox().Intersects(rectangle)) == true)
            {
                return false;
            }

            // Check for resource clumps.
            switch (this.graph.GameLocation)
            {
                case Woods woods:
                    if (woods.stumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return false;
                    }

                    break;
                case Forest forest:
                    if (forest.log is not null && forest.log.occupiesTile(this.X, this.Y))
                    {
                        return false;
                    }

                    break;
                default:
                    if (this.graph.GameLocation.resourceClumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return false;
                    }

                    break;
            }

            // If there's a building here, it must be passable.
            if (this.graph.GameLocation is BuildableGameLocation buildableGameLocation
                && buildableGameLocation.buildings.Any(building => !building.isTilePassable(tileVector)))
            {
                return false;
            }

            return !this.ContainsFurnitureIgnoreRugs() && !this.ContainsAnimal() && !this.ContainsNpc()
                   && !this.ContainsFestivalProp() && !this.IsBlockingBedTile() && !this.ContainsTravellingCart()
                   && !this.ContainsTravellingDesertShop() && !this.BrokenFestivalTile() && !this.ContainsCinema();
        }

        public bool IsUpLeft(AStarNode node)
        {
            return node is not null && this.X == node.X - 1 && this.Y == node.Y - 1;
        }

        public bool IsUpRightTo(AStarNode node)
        {
            return node is not null && this.X == node.X + 1 && this.Y == node.Y - 1;
        }

        public bool IsUpTo(AStarNode node)
        {
            return node is not null && this.X == node.X && this.Y == node.Y - 1;
        }

        public bool IsWater()
        {
            if (this.graph.GameLocation is Submarine && this.X >= 9 && this.X <= 20 && this.Y >= 7 && this.Y <= 11)
            {
                return true;
            }

            if (this.graph.GameLocation.doesTileHaveProperty(this.X, this.Y, "Water", "Back") is null)
            {
                return this.graph.GameLocation.doesTileHaveProperty(this.X, this.Y, "WaterSource", "Back") is not null;
            }

            return true;
        }

        public bool IsWateringCanFillingSource()
        {
            if (this.IsWater() && !this.IsTilePassable())
            {
                return true;
            }

            if (this.graph.GameLocation is BuildableGameLocation buildableGameLocation)
            {
                Building building = buildableGameLocation.getBuildingAt(new Vector2(this.X, this.Y));
                if (building is not null && (building is FishPond || building.buildingType.Value == "Well")
                                         && building.daysOfConstructionLeft.Value <= 0)
                {
                    return true;
                }
            }

            if (this.graph.GameLocation is Submarine && this.X >= 9 && this.X <= 20 && this.Y >= 7 && this.Y <= 11)
            {
                return true;
            }

            if (this.graph.GameLocation.name.Value == "Greenhouse"
                && ((this.X == 9 && this.Y == 7) || (this.X == 10 && this.Y == 7)))
            {
                return true;
            }

            if (this.graph.GameLocation.name.Value == "Railroad" && this.X >= 14 && this.X <= 16 && this.Y >= 55
                && this.Y <= 56)
            {
                return true;
            }

            return false;
        }

        public bool SetBubbleIdRecursively(int bubbleId, bool two = false)
        {
            if (this.BubbleChecked)
            {
                return false;
            }

            this.BubbleChecked = true;

            if (this.BubbleId == 0 || this.TileClear)
            {
                if (two)
                {
                    this.BubbleId2 = bubbleId;
                }
                else
                {
                    this.BubbleId = bubbleId;
                }

                foreach (WalkDirection walkDirection in WalkDirection.SimpleDirections)
                {
                    AStarNode node = this.graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                    node?.SetBubbleIdRecursively(bubbleId, two);
                }

                return true;
            }

            return false;
        }

        public WalkDirection WalkDirectionTo(AStarNode endNode)
        {
            if (endNode is null)
            {
                return WalkDirection.None;
            }

            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                if (this.X == endNode.X + walkDirection.X && this.Y == endNode.Y + walkDirection.Y)
                {
                    return walkDirection;
                }
            }

            return WalkDirection.None;
        }

        /// <summary>
        ///     Gets a list of the nodes in the neighbourhood of this node.
        /// </summary>
        /// <param name="directions">An array of the directions to consider.</param>
        /// <param name="canWalkOnTile">If true, only nodes that can be walked on will be considered. true by default.</param>
        /// <returns>Returns a list of nodes.</returns>
        private List<AStarNode> GetNeighboursInternal(WalkDirection[] directions, bool canWalkOnTile = true)
        {
            List<AStarNode> list = new List<AStarNode>();

            foreach (WalkDirection walkDirection in directions)
            {
                AStarNode neighbour = this.graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                if (neighbour is not null && neighbour.TileClear == canWalkOnTile)
                {
                    list.Add(neighbour);
                }
            }

            return list;
        }
    }
}