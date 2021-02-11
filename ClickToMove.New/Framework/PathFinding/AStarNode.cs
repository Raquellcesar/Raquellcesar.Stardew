// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="AStarNode.cs">
//     Copyright (c) 2021 Raquellcesar Use of this source code is governed by an MIT-style license
//     that can be found in the LICENSE file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework.PathFinding
{
    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.Common;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Characters;
    using StardewValley.Locations;
    using StardewValley.Objects;
    using StardewValley.TerrainFeatures;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using xTile.Dimensions;
    using xTile.Tiles;

    using Rectangle = Microsoft.Xna.Framework.Rectangle;
    using SObject = StardewValley.Object;

    /// <summary>
    ///     The class for nodes used by the <see cref="AStarGraph"/> class. Each node is associated
    ///     to a tile in the map for the <see cref="GameLocation"/> of the graph it belongs to.
    /// </summary>
    internal class AStarNode : IComparable<AStarNode>
    {
        /// <summary>
        ///     The graph to which the node belongs.
        /// </summary>
        private readonly AStarGraph graph;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarNode"/> class.
        /// </summary>
        /// <param name="graph">The <see cref="AStarGraph"/> to which this node belongs.</param>
        /// <param name="x">The x tile coordinate.</param>
        /// <param name="y">The y tile coordinate.</param>
        public AStarNode(AStarGraph graph, int x, int y)
        {
            this.graph = graph;

            this.X = x;
            this.Y = y;
        }

        /// <summary>
        ///     Gets the bounding box for the tile associated to this node.
        /// </summary>
        public Rectangle BoundingBox =>
            new Rectangle(this.X * Game1.tileSize, this.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);

        public bool BubbleChecked { get; set; }

        public int BubbleId { get; set; } = -1;

        public int BubbleId2 { get; set; } = -1;

        /// <summary>
        ///     Gets this node position on the map, centered on the tile.
        /// </summary>
        public Vector2 CenterOnMap =>
            new Vector2(
                (this.X * Game1.tileSize) + (Game1.tileSize / 2),
                (this.Y * Game1.tileSize) + (Game1.tileSize / 2));

        /// <summary>
        ///     Gets or sets a value indicating whether this node is temporarily clear during the
        ///     search for a path. Used to allow occupied nodes as goal nodes in the search for a path.
        /// </summary>
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
        ///     Gets or sets the estimated cost of the cheapest path from this node to the goal.
        /// </summary>
        public int HCost { get; set; }

        /// <summary>
        ///     Gets or sets the previous node in a path.
        /// </summary>
        public AStarNode PreviousNode { get; set; }

        /// <summary>
        ///     Gets a value indicating whether gets if the tle represented by this node is clear.
        /// </summary>
        public bool TileClear
        {
            get
            {
                if (this.FakeTileClear)
                {
                    return true;
                }

                return this.IsTilePassableAndUnoccupied();
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
            if (Game1.CurrentEvent != null)
            {
                switch (this.X)
                {
                    case 18 when this.Y == 31 && Game1.dayOfMonth == 16 && Game1.currentSeason == "fall":
                    case 16 when this.Y == 19 && Game1.dayOfMonth == 27 && Game1.currentSeason == "fall":
                    case 66 when this.Y == 4 && Game1.dayOfMonth == 8 && Game1.currentSeason == "winter":
                    case 103 when this.Y == 28 && Game1.dayOfMonth == 8 && Game1.currentSeason == "winter":
                        return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     The comparison between AStarNodes is made by comparing their <see cref="FCost"/> values.
        /// </remarks>
        public int CompareTo(AStarNode other)
        {
            return this.FCost.CompareTo(other.FCost);
        }

        /// <summary>
        ///     Checks if there's a choppable object at the tile associated with this node.
        /// </summary>
        /// <param name="gameLocation">The <see cref="GameLocation"/> instance.</param>
        /// <returns>
        ///     Returns true if there's something that can be chopped at this node's tile. Returns
        ///     false otherwise.
        /// </returns>
        public bool ConstainsChoppable()
        {
            GameLocation gameLocation = this.graph.GameLocation;

            gameLocation.terrainFeatures.TryGetValue(new Vector2(this.X, this.Y), out TerrainFeature terrainFeature);

            if (terrainFeature is Tree || terrainFeature is FruitTree
                                       || (terrainFeature is Bush bush && bush.IsDestroyable(
                                               gameLocation,
                                               this.X,
                                               this.Y)))
            {
                return true;
            }

            int tileLocationX = this.X * Game1.tileSize;
            int tileLocationY = this.Y * Game1.tileSize;

            foreach (LargeTerrainFeature largeTerrainFeature in gameLocation.largeTerrainFeatures)
            {
                if (largeTerrainFeature is Bush largeBush
                    && largeBush.getRenderBounds(new Vector2(largeBush.tilePosition.X, largeBush.tilePosition.Y))
                        .Contains(tileLocationX, tileLocationY)
                    && largeBush.IsDestroyable(gameLocation, this.X, this.Y))
                {
                    return true;
                }
            }

            return (gameLocation is Forest { log: { } } forest && forest.log.getBoundingBox(forest.log.tile)
                .Contains(tileLocationX, tileLocationY)) || gameLocation.IsStumpAt(this.X, this.Y);
        }

        /// <summary>
        ///     Checks whether there's an animal at the tile represented by this node.
        /// </summary>
        /// <returns>Returns true if there's an animal here. Returns false otherwise.</returns>
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

        /// <summary>
        ///     Checks whether this node represents a tile in the Cinema.
        /// </summary>
        /// <returns>Returns true if this node's tile is a Cinema's tile. Returns false otherwise.</returns>
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

        /// <summary>
        ///     Checks if the Cinema's door is at this node's tile coordinates.
        /// </summary>
        /// <param name="town">The <see cref="Town"/> instance.</param>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns true if the Cinema's door is at this node's tile coordinates. Returns false otherwise.
        /// </returns>
        public bool ContainsCinemaDoor()
        {
            return this.graph.GameLocation.ContainsCinemaDoor(this.X, this.Y);
        }

        /// <summary>
        ///     Checks if the Cinema's ticket office is at this node's tile coordinates.
        /// </summary>
        /// <returns>
        ///     Returns true if the Cinema's ticket office is at this node's tile. Returns false otherwise.
        /// </returns>
        public bool ContainsCinemaTicketOffice()
        {
            return this.graph.GameLocation.ContainsCinemaTicketOffice(this.X, this.Y);
        }

        public bool ContainsFence()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);
            return @object is Fence;
        }

        /// <summary>
        ///     Checks whether there's a festival prop here.
        /// </summary>
        /// <returns>
        ///     Returns true if this node's tile contains a a festival prop. Returns false otherwise.
        /// </returns>
        public bool ContainsFestivalProp()
        {
            return Game1.CurrentEvent is not null
                   && Game1.CurrentEvent.festivalProps.Any(prop => prop.isColliding(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if there is any piece of furniture occupying the tile that this node
        ///     represents. Ignores beds.
        /// </summary>
        /// <returns>Returns true if there is some furniture occupying this node's tile.</returns>
        public bool ContainsFurniture()
        {
            return this.graph.GameLocation is DecoratableLocation decoratableLocation
                   && decoratableLocation.furniture.Any(
                       furniture => furniture is not BedFurniture && furniture
                                        .getBoundingBox(furniture.tileLocation.Value).Intersects(this.TileRectangle));
        }

        /// <summary>
        ///     Checks if there is any piece of furniture occupying the tile that this node
        ///     represents. Ignores beds and rugs.
        /// </summary>
        /// <returns>Returns true if there is some furniture occupying this node's tile.</returns>
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
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Fence fence && fence.isGate;
        }

        /// <summary>
        ///     Checks if there's a minable object at the tile associated with this node.
        /// </summary>
        /// <returns>
        ///     Returns true if there's something that can be mined at this node's tile. Returns
        ///     false otherwise.
        /// </returns>
        public bool ContainsMinable()
        {
            return ClickToMoveHelper.IsMinableAt(this.graph.GameLocation, this.X, this.Y);
        }

        /// <summary>
        ///     Checks if there is an NPC occupying the tile that this node represents.
        /// </summary>
        /// <returns>Returns true if there is some NPC occupying this node's tile.</returns>
        public bool ContainsNpc()
        {
            if (this.graph.GameLocation is Beach && this.graph.OldMariner is not null
                                                 && this.graph.OldMariner.getTileX() == this.X
                                                 && this.graph.OldMariner.getTileY() == this.Y)
            {
                return true;
            }

            return this.graph.GameLocation.isCharacterAtTile(new Vector2(this.X, this.Y)) is not null;
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

        public bool ContainsStump()
        {
            return ClickToMoveHelper.IsStumpAt(this.graph.GameLocation, this.X, this.Y);
        }

        /// <summary>
        ///     Checks for the existence of a stump or boulder at the world location represented by
        ///     this node.
        /// </summary>
        /// <returns>
        ///     Returns true if there is a stump or boulder ate the location represented by this
        ///     node. Returns false otherwise.
        /// </returns>
        public bool ContainsStumpOrBoulder()
        {
            switch (this.graph.GameLocation)
            {
                case Woods woods:
                    {
                        if (woods.stumps.Any(t => t.occupiesTile(this.X, this.Y)))
                        {
                            return true;
                        }

                        break;
                    }

                case Forest forest:
                    {
                        if (forest.log is not null && forest.log.occupiesTile(this.X, this.Y))
                        {
                            return true;
                        }

                        break;
                    }

                default:
                    {
                        if (this.graph.GameLocation.resourceClumps.Any(t => t.occupiesTile(this.X, this.Y)))
                        {
                            return true;
                        }

                        break;
                    }
            }

            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject value);

            return value is not null && value.Name == "Boulder";
        }

        public bool ContainsStumpOrHollowLog()
        {
            switch (this.graph.GameLocation)
            {
                case Woods woods:
                    {
                        if (woods.stumps.Any(t => t.occupiesTile(this.X, this.Y)))
                        {
                            return true;
                        }

                        break;
                    }

                case Forest forest:
                    {
                        if (forest.log is not null && forest.log.occupiesTile(this.X, this.Y))
                        {
                            return true;
                        }

                        break;
                    }

                default:
                    {
                        if (this.graph.GameLocation.resourceClumps.Any(
                            resourceClump => resourceClump.occupiesTile(this.X, this.Y)
                                             && (resourceClump.parentSheetIndex.Value == ResourceClump.hollowLogIndex
                                                 || resourceClump.parentSheetIndex.Value == ResourceClump.stumpIndex)))
                        {
                            return true;
                        }

                        break;
                    }
            }

            return false;
        }

        /// <summary>
        ///     Checks if the travelling cart is occupying the tile that this node represents.
        /// </summary>
        /// <returns>Returns true if the travelling cart is occupying this node's tile.</returns>
        public bool ContainsTravellingCart()
        {
            return this.graph.GameLocation.ContainsTravellingCart(this.X, this.Y);
        }

        /// <summary>
        ///     Checks if the travelling desert shop is occupying the tile that this node represents.
        /// </summary>
        /// <returns>
        ///     Returns true if the travelling desert shop is occupying this node's tile.
        /// </returns>
        public bool ContainsTravellingDesertShop()
        {
            return this.graph.GameLocation.ContainsTravellingDesertShop(this.X, this.Y);
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

        /// <summary>
        ///     Gets the building at the tile associated with this node, if there's one.
        /// </summary>
        /// <returns>
        ///     Returns the building at the tile associated with this node, or null if there isn't
        ///     any build at the tile.
        /// </returns>
        public Building GetBuilding()
        {
            return this.graph.GameLocation is BuildableGameLocation buildableGameLocation
                       ? buildableGameLocation.buildings.FirstOrDefault(
                           building => !building.isTilePassable(new Vector2(this.X, this.Y)))
                       : null;
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

        /// <summary>
        ///     Gets the piece of furniture occupying the tile that this node represents, if any.
        ///     Ignores beds.
        /// </summary>
        /// <returns>
        ///     Returns the piece of furniture occupying the tile that this node represents. Returns
        ///     null if there isn't any furniture at the tile.
        /// </returns>
        public Furniture GetFurniture()
        {
            return this.graph.GameLocation is DecoratableLocation decoratableLocation
                       ? decoratableLocation.furniture.FirstOrDefault(
                           furniture =>
                               furniture is not BedFurniture && furniture.getBoundingBox(furniture.tileLocation.Value)
                                                                 .Intersects(this.TileRectangle))
                       : null;
        }

        /// <summary>
        ///     Gets the piece of furniture occupying the tile that this node represents, if any.
        ///     Ignores beds and rugs.
        /// </summary>
        /// <returns>
        ///     Returns the piece of furniture occupying the tile that this node represents. Returns
        ///     null if there isn't any furniture at the tile.
        /// </returns>
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
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Fence fence && fence.isGate ? fence : null;
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

        /// <summary>
        ///     Gets the neighbour of this node in a specified direction.
        /// </summary>
        /// <param name="walkDirection">The direction to follow.</param>
        /// <param name="canWalkOnTile">If the neighbour sould be walkable.</param>
        /// <returns>
        ///     Returns the neighbour of this node in the given direction respecting the constraint,
        ///     if it exists. If there isn't such a node, returns null.
        /// </returns>
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

            return this.GetNeighboursInternal(WalkDirection.SimpleDirections, canWalkOnTile);
        }

        /// <summary>
        ///     Returns the NPC occupying the tile that this node represents, if there is any.
        /// </summary>
        /// <returns>
        ///     Returns the NPC occupying this node's tile, if there is one, or null if there isn't any.
        /// </returns>
        public NPC GetNpc()
        {
            if (this.graph.GameLocation is Beach && this.graph.OldMariner is not null
                                                 && this.graph.OldMariner.getTileX() == this.X
                                                 && this.graph.OldMariner.getTileY() == this.Y)
            {
                return this.graph.OldMariner;
            }

            return this.graph.GameLocation.isCharacterAtTile(new Vector2(this.X, this.Y));
        }

        /// <summary>
        ///     Gets the object at the tile represented by this node.
        /// </summary>
        /// <returns>The <see cref="SObject"/> at this node's tile.</returns>
        public SObject GetObject()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);
            return @object;
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

        /// <summary>
        ///
        /// </summary>
        /// <returns>Returns true if. Returns false otherwise.</returns>
        public bool IsBlockingBedTile()
        {
            if (this.graph.GameLocation is FarmHouse farmHouse)
            {
                Point bedSpot = farmHouse.getBedSpot();

                if (farmHouse.upgradeLevel == 0)
                {
                    return this.Y == bedSpot.Y - 1 && (this.X == bedSpot.X || this.X == bedSpot.X - 1);
                }

                return this.Y == bedSpot.Y + 2
                       && (this.X == bedSpot.X - 1 || this.X == bedSpot.X || this.X == bedSpot.X + 1);
            }

            return false;
        }

        public bool IsGate()
        {
            this.graph.GameLocation.objects.TryGetValue(new Vector2(this.X, this.Y), out SObject @object);

            return @object is Fence fence && fence.isGate && !this.graph.GameLocation.IsSoloGate(fence);
        }

        /// <summary>
        ///     Checks if the given node is a neighbour of this node.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <returns>
        ///     Returns true if the given node is a neighbour of this node. Returns false otherwise.
        /// </returns>
        public bool IsNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return node.X >= this.X - 1 && node.X <= this.X + 1 && node.Y >= this.Y - 1 && node.Y <= this.Y + 1
                   && !(node.X == this.X && node.Y == this.Y);
        }

        /// <summary>
        ///     Checks if a node is a neighbour of this node in a specified direction.
        /// </summary>
        /// <param name="node">The node to check.</param>
        /// <param name="walkDirection">The direction to check.</param>
        /// <returns>
        ///     Returns true if the given node is adjacent to this node in the given direction.
        ///     Returns false otherwise.
        /// </returns>
        public bool IsNeighbourInDirection(AStarNode node, WalkDirection walkDirection)
        {
            return node is not null && walkDirection != WalkDirection.None && node.X == this.X + walkDirection.X
                   && node.Y == this.Y + walkDirection.Y;
        }

        public bool IsNeighbourNoDiagonals(AStarNode node)
        {
            if (node is null)
            {
                return false;
            }

            return (node.X == this.X && (node.Y == this.Y + 1 || node.Y == this.Y - 1))
                   || (node.Y == this.Y && (node.X == this.X + 1 || node.X == this.X - 1));
        }

        /// <summary>
        ///     Checks if this node is the same as another node.
        /// </summary>
        /// <param name="node">The node to compare to this node.</param>
        /// <returns>
        ///     Returns true if the nodes are the same, i.e. they correspond to the same tile.
        /// </returns>
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
        /// <returns>
        ///     Returns true if the given tile position is passable and free of obstacles in the
        ///     given game location.
        /// </returns>
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
                return @object.isPassable();
            }

            // The tile interior.
            Rectangle rectangle = new Rectangle(
                tileLocation.X + 1,
                tileLocation.Y + 1,
                Game1.tileSize - 2,
                Game1.tileSize - 2);

            // Check for NPCs.
            foreach (NPC npc in this.graph.GameLocation.characters)
            {
                if (!(npc is Pet pet && pet.isSleepingOnFarmerBed)
                    && npc?.GetBoundingBox().Intersects(rectangle) == true)
                {
                    return false;
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

        public bool IsWarp()
        {
            Point nodeCenter = new Point(
                (this.X * Game1.tileSize) + (Game1.tileSize / 2),
                (this.Y * Game1.tileSize) + (Game1.tileSize / 2));
            float warpRange = this.graph.GameLocation.WarpRange();
            foreach (Warp warp in this.graph.GameLocation.warps)
            {
                Point warpPoint = new Point(warp.X * Game1.tileSize, warp.Y * Game1.tileSize);

                if (ClickToMoveHelper.Distance(warpPoint, nodeCenter) < warpRange)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsWater()
        {
            return this.graph.GameLocation.IsWater(this.X, this.Y);
        }

        public bool IsWateringCanFillingSource()
        {
            return this.graph.GameLocation.IsWateringCanFillingSource(this.X, this.Y);
        }

        public void SetBubbleIdRecursively(int bubbleId, bool two = false)
        {
            if (this.BubbleChecked)
            {
                return;
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

                return;
            }

            return;
        }

        /// <summary>
        ///     Returns the <see cref="WalkDirection"/> from the tile represented by this node to
        ///     the given node's tile.
        /// </summary>
        /// <param name="node">The node to walk to.</param>
        /// <returns>The direction from this node to the given node.</returns>
        public WalkDirection WalkDirectionToNeighbour(AStarNode node)
        {
            if (node is null)
            {
                return WalkDirection.None;
            }

            foreach (WalkDirection walkDirection in WalkDirection.Directions)
            {
                if (this.X == node.X + walkDirection.X && this.Y == node.Y + walkDirection.Y)
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
                AStarNode neighbour = this.graph.GetNode(this.X + walkDirection.X, this.Y + walkDirection.Y);
                if (neighbour?.TileClear == canWalkOnTile)
                {
                    list.Add(neighbour);
                }
            }

            return list;
        }
    }
}
