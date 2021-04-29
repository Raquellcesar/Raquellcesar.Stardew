// ------------------------------------------------------------------------------------------------
// <copyright file="AStarNode.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework.PathFinding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

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
    ///     The class for nodes used by the <see cref="AStarGraph"/> class.
    /// </summary>
    internal class AStarNode : IComparable<AStarNode>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarNode"/> class.
        /// </summary>
        /// <param name="graph">The <see cref="AStarGraph"/> to which this node belongs.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        public AStarNode(AStarGraph graph, int tileX, int tileY)
        {
            this.Graph = graph;

            this.X = tileX;
            this.Y = tileY;

            this.GCost = int.MaxValue;
        }

        public bool BubbleChecked { get; set; }

        public int BubbleId { get; set; } = -1;

        public int BubbleId2 { get; set; } = -1;

        public bool FakeTileClear { get; set; }

        /// <summary>
        ///     Gets the estimated total cost of the cheapest path from the start node to the goal
        ///     that goes through this node.
        /// </summary>
        public int FCost => this.GCost + this.HCost;

        /// <summary>
        ///     Gets or sets the cost of the path from the start node to this node.
        /// </summary>
        public int GCost { get; set; }

        /// <summary>
        ///     Gets the graph to which the node belongs.
        /// </summary>
        public AStarGraph Graph { get; private set; }

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

                return this.Graph.IsTileOnMap(this.X, this.Y) && this.IsTilePassableAndUnoccupied();
            }
        }

        /// <summary>
        ///     Gets the rectangle surrounding the tile represented by this node.
        /// </summary>
        public Rectangle TileRectangle =>
            new Rectangle(this.X * Game1.tileSize, this.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

        /// <summary>
        ///     Gets or sets the tile x coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        ///     Gets or sets the tile y coordinate.
        /// </summary>
        public int Y { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        ///     The comparison between AStarNodes is made by comparing their <see cref="FCost"/> values.
        /// </remarks>
        public int CompareTo(AStarNode other)
        {
            return this.FCost.CompareTo(other.FCost);
        }

        /// <summary>
        ///     Checks whether there's an animal at the tile represented by this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there's an animal here. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public bool ContainsAnimal()
        {
            return this.Graph.GameLocation switch
            {
                AnimalHouse animalHouse => animalHouse.animals.Values.Any(
                    animal => animal.getTileX() == this.X && animal.getTileY() == this.Y),
                Farm farm => farm.animals.Values.Any(
                    animal => animal.getTileX() == this.X && animal.getTileY() == this.Y),
                _ => false
            };
        }

        /// <summary>
        ///     Checks if there's a boulder at the tile associated to this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a boulder at the tile associated to this
        ///     node. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsBoulder()
        {
            return this.Graph.GameLocation.IsBoulderAt(this.X, this.Y);
        }

        public bool ContainsBuilding()
        {
            Vector2 position = new Vector2(this.X, this.Y);

            if (this.Graph.GameLocation is BuildableGameLocation buildableGameLocation)
            {
                foreach (Building building in buildableGameLocation.buildings)
                {
                    if (!building.isTilePassable(position))
                    {
                        return true;
                    }
                }
            }

            return this.Graph.GameLocation.map.GetLayer("Buildings").PickTile(
                       new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize),
                       Game1.viewport.Size) is not null;
        }

        /// <summary>
        ///     Checks whether this node represents a tile in the Cinema.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if this node's tile is a Cinema's tile. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public bool ContainsCinema()
        {
            return
                this.Graph.GameLocation is Town
                && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater")
                && ((this.X >= 47 && this.X <= 58 && this.Y >= 17 && this.Y <= 19)
                    || ((this.X == 47 || (this.X >= 55 && this.X <= 58)) && this.Y == 20));
        }

        /// <summary>
        ///     Checks whether there's a festival prop here that blocks the way.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if this node's tile contains a a festival prop
        ///     blocking the way. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsCollidingFestivalProp()
        {
            return Game1.CurrentEvent is not null
                && Game1.CurrentEvent.festivalProps.Any(prop => prop.isColliding(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if there is any piece of furniture occupying the tile that this node
        ///     represents that collides with the farmer.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is some furniture occupying this node's tile
        ///     that collides with the farmer. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsCollidingFurniture()
        {
            Rectangle tileRectangle = this.TileRectangle;

            return this.Graph.GameLocation.furniture.Any(
                    furniture => furniture.furniture_type.Value != Furniture.rug
                                 && furniture.IntersectsForCollision(tileRectangle)
                                 && !Game1.player.TemporaryPassableTiles.Intersects(tileRectangle));
        }

        /// <summary>
        ///     Checks if there is a <see cref="Fence"/> at the tile corresponding to this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there's a <see cref="Fence"/> at the tile corresponding to this node.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsFence()
        {
            return this.Graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object) && @object is Fence;
        }

        public bool ContainsGate()
        {
            Vector2 position = new Vector2(this.X, this.Y);

            if (this.Graph.GameLocation.objects.ContainsKey(position))
            {
                SObject @object = this.Graph.GameLocation.objects[position];
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
        /// <returns>
        ///     Returns <see langword="true"/> if there is some NPC occupying this node's tile.
        /// </returns>
        public bool ContainsNpc()
        {
            if (this.Graph.GameLocation is Beach && this.Graph.OldMariner is not null
                                                 && this.Graph.OldMariner.getTileX() == this.X
                                                 && this.Graph.OldMariner.getTileY() == this.Y)
            {
                return true;
            }

            foreach (NPC npc in this.Graph.GameLocation.characters)
            {
                if ((npc is not Pet pet || !pet.isSleepingOnFarmerBed) && npc.getTileX() == this.X
                                                                       && npc.getTileY() == this.Y)
                {
                    return true;
                }
            }

            return this.Graph.GameLocation.currentEvent?.actors?.Any(
                       npc => npc.getTileX() == this.X && npc.getTileY() == this.Y) == true;
        }

        /// <summary>
        ///     Checks whether there's an <see cref="Event"/> prop here.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if this node's tile contains a an event prop. Returns
        ///     <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsProp()
        {
            return Game1.CurrentEvent is not null
                && Game1.CurrentEvent.props.Any(
                    @object => @object.TileLocation.X == this.X && @object.TileLocation.Y == this.Y);
        }

        public bool ContainsScarecrow()
        {
            Vector2 key = new Vector2(this.X, this.Y);

            if (this.Graph.GameLocation.objects.TryGetValue(key, out SObject @object))
            {
                if (@object.ParentSheetIndex is BigCraftableId.Scarecrow or BigCraftableId.DeluxeScarecrow or BigCraftableId.Rarecrow or BigCraftableId.Rarecrow1 or BigCraftableId.Rarecrow2 or BigCraftableId.Rarecrow3 or BigCraftableId.Rarecrow4 or BigCraftableId.Rarecrow5 or BigCraftableId.Rarecrow6 or BigCraftableId.Rarecrow7)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsSomeKindOfWarp()
        {
            Tile tile = this.Graph.GameLocation.map.GetLayer("Buildings").PickTile(
                new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize),
                Game1.viewport.Size);

            return tile is not null && tile.Properties.Select(property => (string)property.Value).Any(
                       text => text.Contains("Warp"));
        }

        /// <summary>
        ///     Checks for the existence of a stump or boulder at the world location represented by
        ///     this node.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a stump or boulder at the location
        ///     represented by this node. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsStumpOrBoulder()
        {
            return this.Graph.GameLocation.IsStumpOrBoulderAt(new Vector2(this.X, this.Y));
        }

        /// <summary>
        ///     Gets the coordinates of the neighbour of a given node closest to this node.
        /// </summary>
        /// <param name="node">The node whose neighbours we want to check.</param>
        /// <returns>
        ///     Returns the coordinates of the neighbour of <paramref name="node"/> closest to this node.
        /// </returns>
        public Point GetNearestNeighbour(AStarNode node)
        {
            Point neighbour = new Point(node.X, node.Y);

            if (this.X < node.X)
            {
                neighbour.X--;
            }
            else if (this.X > node.X)
            {
                neighbour.X++;
            }

            if (this.Y < node.Y)
            {
                neighbour.Y--;
            }
            else if (this.Y > node.Y)
            {
                neighbour.Y++;
            }

            return neighbour;
        }

        /// <summary>
        ///     Checks if the travelling cart is occupying the tile that this node represents.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the travelling cart is occupying this node's tile.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsTravellingCart()
        {
            return this.Graph.GameLocation is Forest { travelingMerchantBounds: { } } forest
                   && forest.travelingMerchantBounds.Any(
                       travelingMerchantBounds => travelingMerchantBounds.Intersects(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if the travelling desert shop is occupying the tile that this node represents.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the travelling desert shop is occupying this
        ///     node's tile. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool ContainsTravellingDesertShop()
        {
            return this.Graph.GameLocation is Desert desert
                   && desert.IsTravelingMerchantHere()
                   && desert.GetMerchantBounds().Intersects(this.TileRectangle);
        }

        /// <summary>
        ///     Checks if the tile that this node represents contains a tree.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if there is a tree at node's tile. Returns <see
        ///     langword="false"/> otherwise.
        /// </returns>
        public bool ContainsTree()
        {
            this.Graph.GameLocation.terrainFeatures.TryGetValue(
                new Vector2(this.X, this.Y),
                out TerrainFeature terrainFeature);

            return terrainFeature is Tree || terrainFeature is FruitTree;
        }

        public AStarNode CrabPotNeighbour()
        {
            List<AStarNode> neighbours = this.GetNeighbours(true, false);

            foreach (AStarNode neighbour in neighbours)
            {
                if (neighbour.GetObject() is SObject @object && @object.ParentSheetIndex == ObjectId.CrabPot)
                {
                    return neighbour;
                }
            }

            return null;
        }

        public Building GetBuilding()
        {
            Vector2 position = new Vector2(this.X, this.Y);
            if (this.Graph.GameLocation is BuildableGameLocation buildableGameLocation)
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

            this.Graph.GameLocation.terrainFeatures.TryGetValue(key, out TerrainFeature terrainFeature);
            if (terrainFeature is Bush bush)
            {
                return bush;
            }

            foreach (LargeTerrainFeature largeTerrainFeature in this.Graph.GameLocation.largeTerrainFeatures)
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

        /// <summary>
        ///     Gets the <see cref="Chest"/> at the tile associated with this node.
        /// </summary>
        /// <returns>
        ///     The <see cref="Chest"/> at the tile associated with this node, if there is such
        ///     chest. Returns <see langword="null"/> otherwise.
        /// </returns>
        public Chest GetChest()
        {
            this.Graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Chest chest ? chest : null;
        }

        /// <summary>
        ///     Returns the <see cref="CrabPot"/> at this node.
        /// </summary>
        /// <returns>
        ///     The <see cref="CrabPot"/> at this node, if there is one. If not, returns <see langword="null"/>.
        /// </returns>
        public CrabPot GetCrabPot()
        {
            return this.GetObject() as CrabPot;
        }

        /// <summary>
        ///     Gets the furniture occupying the tile that this node represents ignoring rugs.
        /// </summary>
        /// <returns>
        ///     Returns the furniture occupying the tile that this node represents, if it exists and
        ///     it's not a rug. Returns <see langword="null"/> otherwise.
        /// </returns>
        public Furniture GetFurnitureNoRug()
        {
            return this.Graph.GameLocation.furniture.FirstOrDefault(
                furniture => furniture.furniture_type.Value != Furniture.rug
                             && furniture.getBoundingBox(furniture.tileLocation.Value).Intersects(this.TileRectangle));
        }

        public Fence GetGate()
        {
            Vector2 key = new Vector2(this.X, this.Y);

            if (this.Graph.GameLocation.objects.TryGetValue(key, out SObject @object))
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
            if (this.Graph.GameLocation is Farm farm)
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

        public AStarNode GetNearestLandNodeToCrabPot()
        {
            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                Point point = new Point(this.X + walkDirection.X, this.Y + walkDirection.Y);

                if (!this.Graph.GameLocation.isWaterTile(point.X, point.Y))
                {
                    return this.Graph.GetNode(point.X, point.Y);
                }
            }

            return this;
        }

        public AStarNode GetNeighbour(WalkDirection walkDirection, bool canWalkOnTile = true)
        {
            if (walkDirection != WalkDirection.None)
            {
                AStarNode neighbour = this.Graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                if (neighbour is not null && neighbour.TileClear == canWalkOnTile)
                {
                    return neighbour;
                }
            }

            return null;
        }

        public AStarNode GetNeighbourPassable()
        {
            foreach (WalkDirection walkDirection in WalkDirection.CardinalDirections)
            {
                AStarNode neighbour = this.Graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);

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
        /// <param name="includeDiagonals">
        ///     If diagonal directions should be considered. false by default.
        /// </param>
        /// <param name="canWalkOnTile">
        ///     If true, only nodes that can be walked on will be considered. true by default.
        /// </param>
        /// <returns>Returns a list of nodes.</returns>
        public List<AStarNode> GetNeighbours(bool includeDiagonals = false, bool canWalkOnTile = true)
        {
            if (includeDiagonals)
            {
                return this.GetNeighboursInternal(WalkDirection.Directions, canWalkOnTile);
            }

            return this.GetNeighboursInternal(WalkDirection.CardinalDirections, canWalkOnTile);
        }

        public NPC GetNpc()
        {
            if (this.Graph.GameLocation is Beach && this.Graph.OldMariner is not null
                                                 && this.Graph.OldMariner.getTileX() == this.X
                                                 && this.Graph.OldMariner.getTileY() == this.Y)
            {
                return this.Graph.OldMariner;
            }

            foreach (NPC npc in this.Graph.GameLocation.characters)
            {
                if (npc.getTileX() == this.X && npc.getTileY() == this.Y)
                {
                    return npc;
                }
            }

            return this.Graph.GameLocation.currentEvent?.actors?.FirstOrDefault(
                npc => npc.getTileX() == this.X && npc.getTileY() == this.Y);
        }

        /// <summary>
        ///     Gets the <see cref="SObject"/> at the tile represented by this node.
        /// </summary>
        /// <returns>
        ///     Returns the <see cref="SObject"/> at the tile represented by this node, if there is
        ///     one. Returns <see langword="null"/> otherwise.
        /// </returns>
        public SObject GetObject()
        {
            return this.Graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object)
                ? @object
                : null;
        }

        public Warp GetWarp(bool ignoreWarps)
        {
            if (ignoreWarps)
            {
                return null;
            }

            return this.Graph.GameLocation.warps.FirstOrDefault(
                warp => Vector2.Distance(
                    new Vector2(warp.X * Game1.tileSize, warp.Y * Game1.tileSize),
                    this.NodeCenterOnMap) < this.Graph.GameLocation.WarpRange());
        }

        /// <summary>
        ///     Checks whether the tile associated to this node is a blocking bed tile, i.e. a bed
        ///     tile that the path should not traverse.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if this node's tile is a bed tile that the path
        ///     shouldn't go through. Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool IsBlockingBedTile()
        {
            foreach (Furniture furniture in this.Graph.GameLocation.furniture)
            {
                if (furniture is BedFurniture bed && bed.getBoundingBox(bed.TileLocation).Intersects(this.TileRectangle))
                {
                    return this.Graph.ClickToMove.CurrentBed != bed && this.Graph.ClickToMove.TargetBed != bed;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks whether the given node is a diagonal neighbour to this node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given node is a diagonal neighbout to this node.
        ///     Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool IsDiagonalNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return (node.X == this.X - 1 || node.X == this.X + 1) && (node.Y == this.Y - 1 || node.Y == this.Y + 1);
        }

        public bool IsGate()
        {
            this.Graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Fence fence && fence.isGate.Value && !this.Graph.GameLocation.IsSoloGate(fence);
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
        /// <returns>
        ///     Returns <see langword="true"/> if the given node is adjacent to this node in the
        ///     given direction.
        /// </returns>
        public bool IsNeighbourInDirection(AStarNode node, WalkDirection walkDirection)
        {
            return node is not null && walkDirection != WalkDirection.None && node.X == this.X + walkDirection.X
                   && node.Y == this.Y + walkDirection.Y;
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
            return this.Graph.GameLocation.IsTilePassable(this.X, this.Y);
        }

        /// <summary>
        ///     Checks if a tile is passable and not occupied by some obstacle.
        /// </summary>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile position is passable and free of
        ///     obstacles in the given game location.
        /// </returns>
        public bool IsTilePassableAndUnoccupied()
        {
            Vector2 tileVector = new Vector2(this.X, this.Y);

            Location tileLocation = new Location(this.X * Game1.tileSize, this.Y * Game1.tileSize);

            if (!this.Graph.GameLocation.IsTilePassable(tileLocation))
            {
                return false;
            }

            // Is there any object at the tile?
            this.Graph.GameLocation.objects.TryGetValue(tileVector, out SObject @object);

            if (@object is not null)
            {
                return @object.isPassable();
            }

            // The tile interior.
            Rectangle rectangle = new Rectangle(
                tileLocation.X + 1,
                tileLocation.Y + 1,
                Game1.tileSize - 2,
                Game1.tileSize - 2);

            // Check for NPCs, ignoring horse.
            foreach (NPC npc in this.Graph.GameLocation.characters)
            {
                if (!(npc is Pet pet && pet.isSleepingOnFarmerBed)
                    && npc?.GetBoundingBox().Intersects(rectangle) == true)
                {
                    return npc is Horse && Game1.player.isRidingHorse();
                }
            }

            // Check for terrain features.
            if (this.Graph.GameLocation.terrainFeatures.TryGetValue(tileVector, out TerrainFeature terrainFeature)
                && rectangle.Intersects(terrainFeature.getBoundingBox(tileVector))
                && !terrainFeature.isPassable())
            {
                return false;
            }

            // Check for large terrain features.
            if (this.Graph.GameLocation.largeTerrainFeatures?.Any(
                    largeTerrainFeature => largeTerrainFeature.getBoundingBox().Intersects(rectangle)) == true)
            {
                return false;
            }

            // Check for resource clumps.
            switch (this.Graph.GameLocation)
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
                    if (this.Graph.GameLocation.resourceClumps.Any(t => t.occupiesTile(this.X, this.Y)))
                    {
                        return false;
                    }

                    break;
            }

            // If there's a building here, it must be passable.
            if (this.Graph.GameLocation is BuildableGameLocation buildableGameLocation
                && buildableGameLocation.buildings.Any(building => !building.isTilePassable(tileVector)))
            {
                return false;
            }

            return !this.ContainsCollidingFurniture()
                   && !this.ContainsAnimal()
                   && !this.ContainsNpc()
                   && !this.ContainsProp()
                   && !this.ContainsCollidingFestivalProp()
                   && !this.ContainsTravellingCart()
                   && !this.ContainsTravellingDesertShop()
                   && !this.ContainsCinema();
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

        public bool IsVerticalOrHorizontalNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return (node.X == this.X && (node.Y == this.Y + 1 || node.Y == this.Y - 1))
                   || (node.Y == this.Y && (node.X == this.X + 1 || node.X == this.X - 1));
        }

        public bool IsWater()
        {
            if (this.Graph.GameLocation is Submarine && this.X >= 9 && this.X <= 20 && this.Y >= 7 && this.Y <= 11)
            {
                return true;
            }

            if (this.Graph.GameLocation.doesTileHaveProperty(this.X, this.Y, "Water", "Back") is null)
            {
                return this.Graph.GameLocation.doesTileHaveProperty(this.X, this.Y, "WaterSource", "Back") is not null;
            }

            return true;
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

                foreach (WalkDirection walkDirection in WalkDirection.CardinalDirections)
                {
                    AStarNode node = this.Graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                    node?.SetBubbleIdRecursively(bubbleId, two);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Determines the <see cref="WalkDirection"/> when going from this node to the given node.
        /// </summary>
        /// <param name="endNode">The target node.</param>
        /// <returns>
        ///     The <see cref="WalkDirection"/> when going from this node to the given node.
        /// </returns>
        public WalkDirection WalkDirectionTo(AStarNode endNode)
        {
            if (endNode is null)
            {
                return WalkDirection.None;
            }

            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                if (endNode.X == this.X + walkDirection.X && endNode.Y == this.Y + walkDirection.Y)
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
        /// <param name="canWalkOnTile">
        ///     If true, only nodes that can be walked on will be considered. true by default.
        /// </param>
        /// <returns>Returns a list of nodes.</returns>
        private List<AStarNode> GetNeighboursInternal(WalkDirection[] directions, bool canWalkOnTile = true)
        {
            List<AStarNode> list = new List<AStarNode>();

            foreach (WalkDirection walkDirection in directions)
            {
                AStarNode neighbour = this.Graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                if (neighbour is not null && neighbour.TileClear == canWalkOnTile)
                {
                    list.Add(neighbour);
                }
            }

            return list;
        }
    }
}
