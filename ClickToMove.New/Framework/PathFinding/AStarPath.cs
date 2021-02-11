// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Raquellcesar" file="AStarPath.cs">
//     Copyright (c) 2021 Raquellcesar Use of this source code is governed by an MIT-style license
//     that can be found in the LICENSE file or at https://opensource.org/licenses/MIT.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Raquellcesar.Stardew.ClickToMove.Framework.PathFinding
{
    using Raquellcesar.Stardew.Common;

    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    ///     The class for paths returned by the <see cref="AStarGraph"/> class.
    /// </summary>
    internal class AStarPath : IEnumerable<AStarNode>
    {
        /// <summary>
        ///     The internal list that actually contains the path nodes.
        /// </summary>
        private readonly List<AStarNode> nodes = new List<AStarNode>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="AStarPath"/> class, representing a path
        ///     from start node to end node.
        /// </summary>
        /// <remarks>
        ///     The path won't include the start node. If the end node is the same as the start
        ///     node, it will be empty.
        /// </remarks>
        /// <param name="startNode">The start node.</param>
        /// <param name="endNode">The end node.</param>
        public AStarPath(AStarNode startNode, AStarNode endNode)
        {
            for (AStarNode node = endNode; node != startNode; node = node.PreviousNode)
            {
                this.nodes.Add(node);
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
        /// <returns>The node at index i. If the index is out of bounds, returns null.</returns>
        public AStarNode this[int i]
        {
            get
            {
                if (i < 0 || i >= this.nodes.Count)
                {
                    return null;
                }

                return this.nodes[i];
            }
        }

        /// <summary>
        ///     Adds a <see cref="AStarNode"/> at the end of the path.
        /// </summary>
        /// <param name="node">The node to add.</param>
        public void Add(AStarNode node)
        {
            this.nodes.Add(node);
        }

        /// <summary>
        ///     Checks if there's a gate at some point in the path.
        /// </summary>
        /// <returns>Returns true if there exist a gate along the path, false otherwise.</returns>
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

        /// <inheritdoc/>
        public IEnumerator<AStarNode> GetEnumerator()
        {
            return this.nodes.GetEnumerator();
        }

        /// <summary>
        ///     Returns the last <see cref="AStarNode"/> in the path without removing it.
        /// </summary>
        /// <returns>The last node in the path. If the path is empty, returns null.</returns>
        public AStarNode GetLast()
        {
            return this.nodes.Count != 0 ? this.nodes[this.nodes.Count - 1] : null;
        }

        /// <summary>
        ///     Returns the first <see cref="AStarNode"/> in the path without removing it.
        /// </summary>
        /// <returns>The first node in the path. If the path is empty, returns null.</returns>
        public AStarNode GetFirst()
        {
            return this.nodes.Count == 0 ? null : this.nodes[0];
        }

        /// <summary>
        ///     Removes and returns the first <see cref="AStarNode"/> in the path.
        /// </summary>
        /// <returns>The first node in the path. If the path is empty, returns null.</returns>
        public AStarNode RemoveFirst()
        {
            if (this.nodes.Count != 0)
            {
                AStarNode first = this.nodes[0];
                this.nodes.RemoveAt(0);
                return first;
            }

            return null;
        }

        /// <summary>
        ///     Removes and returns the last <see cref="AStarNode"/> in the path.
        /// </summary>
        /// <returns>The last node in the path. If the path is empty, returns null.</returns>
        public AStarNode RemoveLast()
        {
            if (this.nodes.Count != 0)
            {
                AStarNode last = this.nodes[this.nodes.Count - 1];
                this.nodes.RemoveAt(this.nodes.Count - 1);
                return last;
            }

            return null;
        }

        /// <summary>
        ///     Removes right angles from the path, if possible.
        /// </summary>
        /// <param name="endNodesToLeave"></param>
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

            if (indexList.Count > 0)
            {
                for (int i = indexList.Count - 1; i >= 0; i--)
                {
                    this.nodes.RemoveAt(indexList[i]);
                }
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.nodes.GetEnumerator();
        }

        /// <summary>
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private WalkDirection DiagonalWalkDirection(int i)
        {
            AStarNode currentNode = this.nodes[i];
            AStarNode nextNode = this.nodes[i + 1];
            AStarNode nextToNextNode = this.nodes[i + 2];

            if ((currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Left) || currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Down))
                && currentNode.IsNeighbourInDirection(nextToNextNode, WalkDirection.DownLeft))
            {
                if (currentNode.GetNeighbour(WalkDirection.Left) is not null
                    && currentNode.GetNeighbour(WalkDirection.Down) is not null)
                {
                    return WalkDirection.DownLeft;
                }
            }
            else if ((currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Right) || currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Down))
                     && currentNode.IsNeighbourInDirection(nextToNextNode, WalkDirection.DownRight))
            {
                if (currentNode.GetNeighbour(WalkDirection.Right) is not null
                    && currentNode.GetNeighbour(WalkDirection.Down) is not null)
                {
                    return WalkDirection.DownRight;
                }
            }
            else if ((currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Left) || currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Up))
                     && currentNode.IsNeighbourInDirection(nextToNextNode, WalkDirection.UpLeft))
            {
                if (currentNode.GetNeighbour(WalkDirection.Left) is not null
                    && currentNode.GetNeighbour(WalkDirection.Up) is not null)
                {
                    return WalkDirection.UpLeft;
                }
            }
            else if ((currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Right) || currentNode.IsNeighbourInDirection(nextNode, WalkDirection.Up))
                     && currentNode.IsNeighbourInDirection(nextToNextNode, WalkDirection.UpRight))
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
