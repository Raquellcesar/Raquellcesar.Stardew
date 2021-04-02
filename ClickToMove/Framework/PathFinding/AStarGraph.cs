// -----------------------------------------------------------------------
// <copyright file="AStarGraph.cs" company="Raquellcesar">
//     Copyright (c) 2021 Raquellcesar. All rights reserved.
//
//     Use of this source code is governed by an MIT-style license that can be found in the LICENSE
//     file in the project root or at https://opensource.org/licenses/MIT.
// </copyright>
// -----------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework.PathFinding
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.Common;
    using Raquellcesar.Stardew.Common.DataStructures;

    using StardewModdingAPI;

    using StardewValley;
    using StardewValley.Buildings;
    using StardewValley.Locations;

    using xTile;

    /// <summary>
    ///     This class represents the graph of nodes used by the A* search algorithm.
    /// </summary>
    public class AStarGraph
    {
        /// <summary>
        ///     A reference to the oldMariner private field in a <see cref="Beach" /> game location.
        /// </summary>
        private readonly IReflectedField<NPC> oldMariner;

        /// <summary>
        ///     The grid of nodes for this graph.
        /// </summary>
        private AStarNode[,] nodes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarGraph" /> class.
        /// </summary>
        /// <param name="gameLocation">
        ///     The game location associated to this graph.
        /// </param>
        public AStarGraph(GameLocation gameLocation)
        {
            this.GameLocation = gameLocation;

            if (gameLocation is Beach)
            {
                this.oldMariner = ClickToMoveManager.Reflection.GetField<NPC>(gameLocation, "oldMariner");
            }

            this.Init();
        }

        /// <summary>
        ///     Gets the node corresponding to the player position.
        /// </summary>
        public AStarNode FarmerNode =>
            this.GetNode(
                (int)Math.Floor(Game1.player.position.X / Game1.tileSize),
                (int)Math.Floor(Game1.player.position.Y / Game1.tileSize));

        /// <summary>
        ///     Gets the node corresponding to the player position considering an offset of half tile.
        /// </summary>
        public AStarNode FarmerNodeOffset
        {
            get
            {
                float playerTileX = (Game1.player.position.X + (Game1.tileSize / 2f)) / Game1.tileSize;
                float playerTileY = (Game1.player.position.Y + (Game1.tileSize / 2f)) / Game1.tileSize;
                return this.GetNode((int)playerTileX, (int)playerTileY);
            }
        }

        /// <summary>
        ///     Gets the <see cref="StardewValley.GameLocation" /> to which this graph is associated.
        /// </summary>
        public GameLocation GameLocation { get; }

        /// <summary>
        ///     Gets the Old Mariner NPC.
        /// </summary>
        public NPC OldMariner => this.oldMariner?.GetValue();

        /// <summary>
        ///     Computes a path between the two specified nodes.
        /// </summary>
        /// <param name="startNode">The start node.</param>
        /// <param name="endNode">The end node.</param>
        /// <returns>A path from the start node to the end node.</returns>
        public AStarPath FindPath(AStarNode startNode, AStarNode endNode)
        {
            if (startNode is null || endNode is null)
            {
                return null;
            }

            startNode.GCost = 0;
            startNode.HCost = AStarGraph.Heuristic(startNode, endNode);
            startNode.PreviousNode = null;

            // The set of discovered nodes that may need to be expanded.
            // Initially, only the start node is known.
            FastBinaryHeap<AStarNode> openSet = new FastBinaryHeap<AStarNode>();
            openSet.Push(startNode);

            // The set of nodes already expanded.
            HashSet<AStarNode> closedSet = new HashSet<AStarNode>();

            while (!openSet.IsEmpty())
            {
                AStarNode currentNode = openSet.Pop();

                if (currentNode == endNode)
                {
                    return new AStarPath(startNode, endNode);
                }

                closedSet.Add(currentNode);

                foreach (AStarNode neighbour in currentNode.GetNeighbours())
                {
                    if (closedSet.Contains(neighbour)
                        || (this.GameLocation is FarmHouse && !endNode.IsBlockingBedTile()
                                                       && neighbour.IsBlockingBedTile()))
                    {
                        continue;
                    }

                    int gCost = currentNode.GCost + 1;

                    bool visited = openSet.Contains(neighbour);

                    if (gCost < neighbour.GCost || !visited)
                    {
                        // This path to neighbour is better than any previous one. Record it!
                        neighbour.GCost = gCost;
                        neighbour.HCost = AStarGraph.Heuristic(neighbour, endNode);
                        neighbour.PreviousNode = currentNode;

                        if (visited)
                        {
                            openSet.Heapify(neighbour);
                        }
                        else
                        {
                            openSet.Push(neighbour);
                        }
                    }
                }
            }

            // Open set is empty but goal was never reached.
            return null;
        }

        public AStarPath FindPathToNeighbourDiagonalWithBubbleCheck(AStarNode startNode, AStarNode endNode)
        {
            AStarPath pathWithBubbleCheck = this.FindPathWithBubbleCheck(startNode, endNode);

            if (pathWithBubbleCheck is not null)
            {
                return pathWithBubbleCheck;
            }

            if (endNode.FakeTileClear)
            {
                double minDistance = double.MaxValue;
                AStarNode nearestNode = null;
                foreach (WalkDirection walkDirection in WalkDirection.DiagonalDirections)
                {
                    AStarNode node = this.GetNode(endNode.X + walkDirection.X, endNode.Y + walkDirection.Y);

                    if (node is not null && node.TileClear)
                    {
                        double distance = ClickToMoveHelper.SquaredEuclideanDistance(
                            startNode.X,
                            startNode.Y,
                            node.X,
                            node.Y);

                        if (distance < minDistance)
                        {
                            nearestNode = node;
                            minDistance = distance;
                        }
                    }
                }

                if (nearestNode is not null)
                {
                    return this.FindPathWithBubbleCheck(startNode, nearestNode);
                }
            }

            return null;
        }

        public AStarPath FindPathWithBubbleCheck(AStarNode startNode, AStarNode endNode)
        {
            if (startNode is null || endNode is null)
            {
                return null;
            }

            if (endNode.BubbleId == 0)
            {
                return this.FindPath(startNode, endNode);
            }

            startNode.BubbleId = 0;
            if (endNode.BubbleId == -1 && this.PathExists(startNode, endNode))
            {
                return this.FindPath(startNode, endNode);
            }

            this.ResetBubbles(false, true);

            endNode.SetBubbleIdRecursively(0, true);

            if (startNode.BubbleId2 == endNode.BubbleId2)
            {
                this.MergeBubbleId2IntoBubbleId();
                return this.FindPath(startNode, endNode);
            }

            return null;
        }

        public AStarNode GetNearestLandNodePerpendicularToWaterSource(AStarNode nodeClicked)
        {
            AStarNode result = nodeClicked;

            AStarNode node;
            if (this.FarmerNodeOffset.X == nodeClicked.X
                || (this.FarmerNodeOffset.Y != nodeClicked.Y && Math.Abs(nodeClicked.X - this.FarmerNodeOffset.X)
                    > Math.Abs(nodeClicked.Y - this.FarmerNodeOffset.Y)))
            {
                if (nodeClicked.Y > this.FarmerNodeOffset.Y)
                {
                    for (int i = nodeClicked.Y; i >= this.FarmerNodeOffset.Y; i--)
                    {
                        node = this.GetNode(nodeClicked.X, i);
                        if (node is not null && node.TileClear && !node.IsWateringCanFillingSource())
                        {
                            return result;
                        }

                        result = node;
                    }
                }
                else
                {
                    for (int i = nodeClicked.Y; i <= this.FarmerNodeOffset.Y; i++)
                    {
                        node = this.GetNode(nodeClicked.X, i);
                        if (node is not null && node.TileClear && !node.IsWateringCanFillingSource())
                        {
                            return result;
                        }

                        result = node;
                    }
                }
            }
            else if (nodeClicked.X > this.FarmerNodeOffset.X)
            {
                for (int i = nodeClicked.X; i >= this.FarmerNodeOffset.X; i--)
                {
                    node = this.GetNode(i, nodeClicked.Y);
                    if (node is not null && node.TileClear && !node.IsWateringCanFillingSource())
                    {
                        return result;
                    }

                    result = node;
                }
            }
            else
            {
                for (int i = nodeClicked.X; i <= this.FarmerNodeOffset.X; i++)
                {
                    node = this.GetNode(i, nodeClicked.Y);
                    if (node is not null && node.TileClear && !node.IsWateringCanFillingSource())
                    {
                        return result;
                    }

                    result = node;
                }
            }

            node = this.GetNodeNearestWaterSource(nodeClicked) ?? this.GetNodeNearestWaterSource(this.FarmerNodeOffset);

            return node;
        }

        public Point GetNearestTileNextToBuilding(Building building)
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

            Point tile = this.GetTileNextToBuilding(tileX, tileY);

            if (tile != Point.Zero)
            {
                return tile;
            }

            // There is no direct path to the nearest tile, let's search for an alternative around it.
            List<Point> tilesAroundBuilding = building.GetBorderTilesList();

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

                tile = this.GetTileNextToBuilding(
                    tilesAroundBuilding[previousIndex].X,
                    tilesAroundBuilding[previousIndex].Y);

                if (tile != Point.Zero)
                {
                    return tile;
                }

                if (nextIndex > tilesAroundBuilding.Count - 1)
                {
                    nextIndex -= tilesAroundBuilding.Count;
                }

                tile = this.GetTileNextToBuilding(
                    tilesAroundBuilding[nextIndex].X,
                    tilesAroundBuilding[nextIndex].Y);

                if (tile != Point.Zero)
                {
                    return tile;
                }
            }

            return new Point(Game1.player.getTileX(), Game1.player.getTileY());
        }

        /// <summary>
        ///     Gets the node for the tile with coordinates (x, y).
        /// </summary>
        /// <param name="x">The x tile coordinate.</param>
        /// <param name="y">The y tile coordinate.</param>
        /// <returns>
        ///     Returns the node at the tile with coordinates (x, y), if that tile exists in the map for the game location
        ///     associated to the graph. Otherwise, returns <see langword="null"/>.
        /// </returns>
        public AStarNode GetNode(int x, int y)
        {
            return x >= 0 && x < this.nodes.GetLength(0) && y >= 0 && y < this.nodes.GetLength(1)
                       ? this.nodes[x, y]
                       : null;
        }

        public AStarNode GetNodeNearestWaterSource(AStarNode node)
        {
            List<AStarNode> list = new List<AStarNode>();

            for (int i = 1; i < 30; i++)
            {
                AStarNode goalNode = this.GetNode(node.X + i, node.Y);

                if (goalNode is not null && goalNode.TileClear && !goalNode.IsWateringCanFillingSource())
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X - i, node.Y);
                if (goalNode is not null && goalNode.TileClear && !goalNode.IsWateringCanFillingSource())
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X, node.Y + i);
                if (goalNode is not null && goalNode.TileClear && !goalNode.IsWateringCanFillingSource())
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X, node.Y - i);
                if (goalNode is not null && goalNode.TileClear && !goalNode.IsWateringCanFillingSource())
                {
                    list.Add(goalNode);
                }

                if (list.Count > 0)
                {
                    break;
                }
            }

            if (list.Count == 0)
            {
                return null;
            }

            int minIndex = 0;
            float minDistance = float.MaxValue;
            for (int i = 1; i < list.Count; i++)
            {
                float distance = Vector2.Distance(Game1.player.OffsetPositionOnMap(), list[i].NodeCenterOnMap);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            int x = node.X;
            int y = node.Y;

            if (list[minIndex].X != node.X)
            {
                x = list[minIndex].X <= node.X ? list[minIndex].X + 1 : list[minIndex].X - 1;
            }
            else
            {
                y = list[minIndex].Y <= node.Y ? list[minIndex].Y + 1 : list[minIndex].Y - 1;
            }

            return this.GetNode(x, y);
        }

        /// <summary>
        ///     (Re)Initializes the grid of nodes for this graph.
        /// </summary>
        public void Init()
        {
            int layerWidth = this.GameLocation.map.Layers[0].LayerWidth;
            int layerHeight = this.GameLocation.map.Layers[0].LayerHeight;
            this.nodes = new AStarNode[layerWidth, layerHeight];
            for (int i = 0; i < layerWidth; i++)
            {
                for (int j = 0; j < layerHeight; j++)
                {
                    this.nodes[i, j] = new AStarNode(this, i, j);
                }
            }
        }

        /// <summary>
        /// Checks if a tile is in the map associated to this graph's <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        /// Returns <see langword="true"/> if the given tile is in the map associated to this graph's <see cref="GameLocation"/>.
        /// Returns <see langword="false"/> otherwise.
        /// </returns>
        public bool IsTileOnMap(int tileX, int tileY)
        {
            return this.GameLocation.isTileOnMap(tileX, tileY);
        }

        public void RefreshBubbles()
        {
            this.ResetBubbles(true, true);

            if (this.FarmerNode is not null)
            {
                this.FarmerNodeOffset?.SetBubbleIdRecursively(0);
            }
        }

        /// <summary>
        ///     Clears the bubble information from the graph nodes.
        /// </summary>
        /// <param name="one">Whether to clear the first bubble id.</param>
        /// <param name="two">Whether to clear the second bubble id.</param>
        public void ResetBubbles(bool one = true, bool two = false)
        {
            if (this.GameLocation.map is null)
            {
                return;
            }

            for (int i = 0; i < this.GameLocation.map.Layers[0].LayerWidth; i++)
            {
                for (int j = 0; j < this.GameLocation.map.Layers[0].LayerHeight; j++)
                {
                    this.nodes[i, j].BubbleChecked = false;

                    if (one)
                    {
                        this.nodes[i, j].BubbleId = -1;
                    }

                    if (two)
                    {
                        this.nodes[i, j].BubbleId2 = -1;
                    }
                }
            }
        }

        /// <summary>
        ///     Computes the Manhattan distance from the current node to the end node.
        /// </summary>
        /// <param name="current">The current node.</param>
        /// <param name="end">The end node.</param>
        /// <returns>Returns the Manhattan distance from the current node to the end node.</returns>
        private static int Heuristic(AStarNode current, AStarNode end)
        {
            return Math.Abs(current.X - end.X) + Math.Abs(current.Y - current.Y);
        }

        private Point GetTileNextToBuilding(int tileX, int tileY)
        {
            AStarNode tileNode = this.GetNode(tileX, tileY);

            if (tileNode is not null)
            {
                tileNode.FakeTileClear = true;

                AStarPath path = this.FindPathWithBubbleCheck(this.FarmerNodeOffset, tileNode);

                if (path is not null && path.Count > 0)
                {
                    return new Point(tileX, tileY);
                }

                tileNode.FakeTileClear = false;
            }

            return Point.Zero;
        }

        private void MergeBubbleId2IntoBubbleId()
        {
            for (int i = 0; i < this.GameLocation.map.Layers[0].LayerWidth; i++)
            {
                for (int j = 0; j < this.GameLocation.map.Layers[0].LayerHeight; j++)
                {
                    if (this.nodes[i, j].BubbleId2 == 0)
                    {
                        this.nodes[i, j].BubbleId = 0;
                        this.nodes[i, j].BubbleId2 = -1;
                    }

                    this.nodes[i, j].BubbleChecked = false;
                }
            }
        }

        private bool PathExists(AStarNode start, AStarNode end)
        {
            if (start.BubbleId == end.BubbleId)
            {
                return true;
            }

            if (end.BubbleId == -1 && end.FakeTileClear)
            {
                foreach (WalkDirection walkDirection in WalkDirection.SimpleDirections)
                {
                    AStarNode endNeighbour = this.GetNode(end.X + walkDirection.X, end.Y + walkDirection.Y);
                    if (endNeighbour is not null && endNeighbour.BubbleId == start.BubbleId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
