// -----------------------------------------------------------------------
// <copyright file="AStarPath.cs" company="Raquellcesar">
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
    using System.Collections;
    using System.Collections.Generic;

    using Raquellcesar.Stardew.Common;

    /// <summary>
    ///     The class for paths returned by the <see cref="AStarGraph" /> class.
    /// </summary>
    public class AStarPath : IEnumerable<AStarNode>
    {
        /// <summary>
        ///     The internal list that actually contains the path nodes.
        /// </summary>
        private readonly List<AStarNode> nodes = new List<AStarNode>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarPath"/> class,
        ///     representing a path from start node to end node.
        /// </summary>
        /// <param name="startNode">The start node.</param>
        /// <param name="endNode">The end node.</param>
        public AStarPath(AStarNode startNode, AStarNode endNode)
        {
            for (AStarNode aStarNode = endNode; aStarNode != startNode; aStarNode = aStarNode.PreviousNode)
            {
                this.nodes.Add(aStarNode);
            }

            this.nodes.Reverse();
        }

        /// <summary>
        ///     Returns the number of nodes in the path.
        /// </summary>
        public int Count => this.nodes.Count;

        /// <summary>
        ///     Allows access to the nodes in the path using [] notation.
        /// </summary>
        /// <param name="i">The index of the node to access.</param>
        /// <returns>The node at index i.</returns>
        /// <exception cref="IndexOutOfRangeException">If the index is out of bounds.</exception>
        public AStarNode this[int i]
        {
            get
            {
                if (i < 0 || i >= this.nodes.Count)
                {
                    throw new IndexOutOfRangeException(
                        $"The index must be between 0 and {this.nodes.Count.ToString()}.");
                }

                return this.nodes[i];
            }
        }

        /// <summary>
        ///     Adds a node at the end of the path.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public void Add(AStarNode node)
        {
            this.nodes.Add(node);
        }

        /// <summary>
        ///     Checks if there's a gate at some point in the path.
        /// </summary>
        /// <returns>Returns <see langword="true"/> if there exist a gate along the path, false otherwise.</returns>
        public AStarNode ContainsGate()
        {
            foreach (AStarNode node in this.nodes)
            {
                if (node.IsGate())
                {
                    return node;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public IEnumerator<AStarNode> GetEnumerator()
        {
            return this.nodes.GetEnumerator();
        }

        public void RemoveAt(int i)
        {
            this.nodes.RemoveAt(i);
        }

        /// <summary>
        /// Removes the last node from the path.
        /// </summary>
        public void RemoveLastNode()
        {
            this.nodes.RemoveAt(this.nodes.Count - 1);
        }

        public void SmoothRightAngles(int endNodesToLeave = 1)
        {
            // Constructs the list of nodes to remove, i.e. nodes that connect the previous node
            // to a node in a diagonal direction.
            List<int> indexList = new List<int>();
            for (int i = 0; i < this.nodes.Count - 1 - endNodesToLeave; i++)
            {
                if (this.DiagonalWalkDirection(i) != WalkDirection.None)
                {
                    i++;
                    indexList.Add(i);
                }
            }

            // Now, just remove the conecting nodes, starting from the end for efficiency.
            if (indexList.Count > 0)
            {
                for (int i = indexList.Count - 1; i >= 0; i--)
                {
                    this.nodes.RemoveAt(indexList[i]);
                }
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.nodes.GetEnumerator();
        }

        private WalkDirection DiagonalWalkDirection(int i)
        {
            AStarNode currentNode = this.nodes[i];
            AStarNode nextNode = this.nodes[i + 1];
            AStarNode nextToNextNode = this.nodes[i + 2];

            if ((nextNode.IsLeftTo(currentNode) || nextNode.IsDownTo(currentNode))
                && nextToNextNode.IsDownLeft(currentNode))
            {
                if (currentNode.GetNeighbour(WalkDirection.Left) is not null
                    && currentNode.GetNeighbour(WalkDirection.Down) is not null)
                {
                    return WalkDirection.DownLeft;
                }
            }
            else if ((nextNode.IsRightTo(currentNode) || nextNode.IsDownTo(currentNode))
                     && nextToNextNode.IsDownRightTo(currentNode))
            {
                if (currentNode.GetNeighbour(WalkDirection.Right) is not null
                    && currentNode.GetNeighbour(WalkDirection.Down) is not null)
                {
                    return WalkDirection.DownRight;
                }
            }
            else if ((nextNode.IsLeftTo(currentNode) || nextNode.IsUpTo(currentNode))
                     && nextToNextNode.IsUpLeft(currentNode))
            {
                if (currentNode.GetNeighbour(WalkDirection.Left) is not null
                    && currentNode.GetNeighbour(WalkDirection.Up) is not null)
                {
                    return WalkDirection.UpLeft;
                }
            }
            else if ((nextNode.IsRightTo(currentNode) || nextNode.IsUpTo(currentNode))
                     && nextToNextNode.IsUpRightTo(currentNode))
            {
                if (currentNode.GetNeighbour(WalkDirection.Right) is not null
                    && currentNode.GetNeighbour(WalkDirection.Up) is not null)
                {
                    return WalkDirection.UpRight;
                }
            }

            return WalkDirection.None;
        }
    }
}