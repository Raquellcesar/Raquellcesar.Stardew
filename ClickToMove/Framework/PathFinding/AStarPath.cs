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
        ///     Gets the number of nodes in the path.
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

        /// <summary>
        ///     Returns the first <see cref="AStarNode"/> in the path.
        /// </summary>
        /// <returns>The first node in the path. If the path is empty, returns null.</returns>
        public AStarNode GetFirst()
        {
            return this.nodes.Count == 0 ? null : this.nodes[0];
        }

        /// <summary>
        ///     Returns the last <see cref="AStarNode"/> in the path.
        /// </summary>
        /// <returns>The last node in the path. If the path is empty, returns null.</returns>
        public AStarNode GetLast()
        {
            return this.nodes.Count != 0 ? this.nodes[this.nodes.Count - 1] : null;
        }

        /// <summary>
        ///     Removes the first <see cref="AStarNode"/> in the path.
        ///     If the path is empty, the method does nothing.
        /// </summary>
        public void RemoveFirst()
        {
            if (this.nodes.Count != 0)
            {
                this.nodes.RemoveAt(0);
            }
        }

        /// <summary>
        ///     Removes the last <see cref="AStarNode"/> in the path.
        ///     If the path is empty, the method does nothing.
        /// </summary>
        public void RemoveLast()
        {
            if (this.nodes.Count != 0)
            {
                this.nodes.RemoveAt(this.nodes.Count - 1);
            }
        }

        /// <summary>
        ///     Removes right angles from the path, if possible.
        /// </summary>
        /// <param name="endNodesToLeave">
        ///     The number of nodes to ignore at the end of the path.
        ///     It must be greater than 0. Defaults to 1.
        /// </param>
        /// <remarks>
        ///     If the argument is inferior to 1, the default value of 1 is used instead.
        /// </remarks>
        public void SmoothRightAngles(int endNodesToLeave = 1)
        {
            // Constructs the list of nodes to remove, i.e. nodes that connect the previous node
            // to a node in a diagonal direction.
            List<int> indexList = new List<int>();
            for (int i = 1; i < this.nodes.Count - 1 - Math.Max(endNodesToLeave, 1); i++)
            {
                if (this.CanRemoveNode(i))
                {
                    indexList.Add(i);
                    i++;
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

        /// <summary>
        ///     Checks if a node can be removed from the path when smothing the path.
        ///     A node can be safely removed from the path if it connects the previous node to a
        ///     diagonal neighbour and the nodes leading that diagonal neighbour are both clear.
        /// </summary>
        /// <param name="i">The index of the node.</param>
        /// <returns>
        ///     Returns <see langword="true"/> if the node next can be removed from the path
        ///     safely. Returns <see langword="false"/> otherwise.
        /// </returns>
        private bool CanRemoveNode(int i)
        {
            AStarNode previousNode = this.nodes[i - 1];
            AStarNode currentNode = this.nodes[i];
            AStarNode nextNode = this.nodes[i + 1];

            foreach (WalkDirection walkDirection in WalkDirection.DiagonalDirections)
            {
                if (previousNode.X + walkDirection.X == nextNode.X && previousNode.Y + walkDirection.Y == nextNode.Y)
                {
                    AStarNode neighbour = null;
                    if (previousNode.X == currentNode.X)
                    {
                        neighbour = previousNode.Graph.GetNode(previousNode.X + walkDirection.X, previousNode.Y);
                    }
                    else if (previousNode.Y == currentNode.Y)
                    {
                        neighbour = previousNode.Graph.GetNode(previousNode.X, previousNode.Y + walkDirection.Y);
                    }

                    return neighbour is not null && neighbour.TileClear;
                }
            }

            return false;
        }
    }
}
