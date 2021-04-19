// ------------------------------------------------------------------------------------------------
// <copyright file="AStarGraph.cs" company="Raquellcesar">
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

    using Microsoft.Xna.Framework;

    using Raquellcesar.Stardew.Common;
    using Raquellcesar.Stardew.Common.DataStructures;

    using StardewValley;
    using StardewValley.Buildings;

    /// <summary>
    ///     This class represents the graph of nodes used by the A* search algorithm.
    /// </summary>
    internal class AStarGraph
    {
        /// <summary>
        ///     The grid of nodes for this graph.
        /// </summary>
        private AStarNode[,] nodes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarGraph"/> class.
        /// </summary>
        /// <param name="clickToMove">
        ///     The <see cref="Framework.ClickToMove"/> to which this graph belongs.
        /// </param>
        public AStarGraph(ClickToMove clickToMove)
        {
            this.ClickToMove = clickToMove;

            this.Init();
        }

        /// <summary>
        ///     Gets the <see cref="Framework.ClickToMove"/> object to which this graph belongs.
        /// </summary>
        public ClickToMove ClickToMove { get; }

        /// <summary>
        ///     Gets the node corresponding to the player's position.
        /// </summary>
        public AStarNode FarmerNode
        {
            get
            {
                Point playerTile = Game1.player.getTileLocationPoint();
                return this.GetNode(playerTile.X, playerTile.Y);
            }
        }

        /// <summary>
        ///     Gets the <see cref="StardewValley.GameLocation"/> to which this graph is associated.
        /// </summary>
        public GameLocation GameLocation => this.ClickToMove.GameLocation;

        /// <summary>
        ///     Gets the Old Mariner NPC.
        /// </summary>
        public NPC OldMariner => this.ClickToMove.OldMariner;

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

            // The set of discovered nodes that may need to be expanded. Initially, only the start
            // node is known.
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
                    if (closedSet.Contains(neighbour) || neighbour.IsBlockingBedTile())
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

        /// <summary>
        ///     Gets the water source node that neighbours land that is on the same column or row as
        ///     the given water source node or the Farmer and is closest to the clicked node.
        /// </summary>
        /// <param name="waterSourceNode">The water source node clicked.</param>
        /// <returns>
        ///     The water source node that neighbours land that is on the same column or row as the
        ///     given water source node or the Farmer and is closest to the clicked node.
        /// </returns>
        public AStarNode GetClosestCoastNode(AStarNode waterSourceNode)
        {
            int distanceX = this.FarmerNode.X - waterSourceNode.X;
            int distanceY = this.FarmerNode.Y - waterSourceNode.Y;

            int deltaX = Math.Sign(distanceX);
            int deltaY = Math.Sign(distanceY);

            int tile1X = waterSourceNode.X + deltaX;
            int tile1Y = waterSourceNode.Y;
            int tile2X = waterSourceNode.X;
            int tile2Y = waterSourceNode.Y + deltaY;
            AStarNode coastNode1 = waterSourceNode;
            AStarNode coastNode2 = waterSourceNode;
            for (int i = 0; i < Math.Abs(distanceX) + Math.Abs(distanceY); i++)
            {
                // Go through the tiles in the same row as the clicked node, then the tiles in the
                // same column as the Farmer.
                if (distanceX != 0)
                {
                    if (tile1X != this.FarmerNode.X)
                    {
                        AStarNode node = this.GetNode(tile1X, tile1Y);
                        if (node is not null && node.TileClear)
                        {
                            return coastNode1;
                        }

                        coastNode1 = node;

                        tile1X += deltaX;
                    }
                    else
                    {
                        AStarNode node = this.GetNode(tile1X, tile1Y);
                        if (node is not null && node.TileClear)
                        {
                            return coastNode1;
                        }

                        coastNode1 = node;

                        tile1Y += deltaY;
                    }
                }

                // Go through the tiles in the same column as the clicked node, then the tiles in
                // the same row as the Farmer.
                if (distanceY != 0)
                {
                    if (tile2Y != this.FarmerNode.Y)
                    {
                        AStarNode node = this.GetNode(tile2X, tile2Y);
                        if (node is not null && node.TileClear)
                        {
                            return coastNode2;
                        }

                        coastNode2 = node;

                        tile2Y += deltaY;
                    }
                    else
                    {
                        AStarNode node = this.GetNode(tile2X, tile2Y);
                        if (node is not null && node.TileClear)
                        {
                            return coastNode2;
                        }

                        coastNode2 = node;

                        tile2X += deltaX;
                    }
                }
            }

            return null;
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
        ///     Gets the node for the tile with coordinates ( <paramref name="tileX"/>, <paramref name="tileY"/>).
        /// </summary>
        /// <param name="tileX">The x tile coordinate.</param>
        /// <param name="tileY">The y tile coordinate.</param>
        /// <returns>
        ///     Returns the node at the tile with coordinates ( <paramref name="tileX"/>, <paramref
        ///     name="tileY"/>), if that tile exists in the map for the game location associated to
        ///     the graph. Otherwise, returns <see langword="null"/>.
        /// </returns>
        public AStarNode GetNode(int tileX, int tileY)
        {
            return tileX >= 0 && tileX < this.nodes.GetLength(0) && tileY >= 0 && tileY < this.nodes.GetLength(1)
                       ? this.nodes[tileX, tileY]
                       : null;
        }

        public AStarNode GetNodeNearestWaterSource(AStarNode node)
        {
            List<AStarNode> list = new List<AStarNode>();

            for (int i = 1; i < 30; i++)
            {
                AStarNode goalNode = this.GetNode(node.X + i, node.Y);

                if (goalNode is not null && goalNode.TileClear)
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X - i, node.Y);
                if (goalNode is not null && goalNode.TileClear)
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X, node.Y + i);
                if (goalNode is not null && goalNode.TileClear)
                {
                    list.Add(goalNode);
                }

                goalNode = this.GetNode(node.X, node.Y - i);
                if (goalNode is not null && goalNode.TileClear)
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
        ///     Checks if a tile is in the map associated to this graph's <see cref="GameLocation"/>.
        /// </summary>
        /// <param name="tileX">The tile x coordinate.</param>
        /// <param name="tileY">The tile y coordinate.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the given tile is in the map associated to this
        ///     graph's <see cref="GameLocation"/>. Returns <see langword="false"/> otherwise.
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
                this.FarmerNode?.SetBubbleIdRecursively(0);
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

                AStarPath path = this.FindPathWithBubbleCheck(this.FarmerNode, tileNode);

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
