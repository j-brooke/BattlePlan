using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Path
{
    public static class AStar
    {
        /// <summary>
        /// Quick hack implementation of the A* pathfinding algorithm (which might not even be the right
        /// algorithm for our purposes).  There are many ways this code should be improved, but you've
        /// got to start somewhere.  These websites are useful resources.
        ///
        ///   https://theory.stanford.edu/~amitp/GameProgramming/AStarComparison.html
        ///   https://qiao.github.io/PathFinding.js/visual/
        /// </summary>
        public static IList<T> FindPath<T>(IPathGraph<T> graph, T startNode, T destNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            // TODO: rewrite this with more efficient data structures.  This approach is around n^2*log(n).
            var openSet = new Dictionary<T,PathPiece<T>>();
            var closedSet = new Dictionary<T,PathPiece<T>>();
            PathPiece<T> destPiece = null;

            var startPiece = new PathPiece<T>(startNode)
            {
                EstimatedRemainingCost = graph.EstimatedDistance(startNode, destNode),
            };
            openSet.Add(startPiece.Node, startPiece);

            while (openSet.Count>0 && destPiece==null)
            {
                var currentPiece = openSet.Values
                    .OrderByDescending( (piece) => (piece.CostFromStart+piece.EstimatedRemainingCost) )
                    .Last();
                openSet.Remove(currentPiece.Node);
                closedSet.Add(currentPiece.Node, currentPiece);

                var neighbors = graph.Neighbors(currentPiece.Node);
                foreach (var neighborNode in neighbors)
                {
                    PathPiece<T> neighborPiece = null;
                    bool inOpenSet = openSet.TryGetValue(neighborNode, out neighborPiece);
                    bool inClosedSet = !inOpenSet && closedSet.TryGetValue(neighborNode, out neighborPiece);

                    double costToNeighbor = currentPiece.CostFromStart + graph.Cost(currentPiece.Node, neighborNode);
                    if (neighborPiece == null)
                    {
                        neighborPiece = new PathPiece<T>(neighborNode)
                        {
                            CostFromStart = costToNeighbor,
                            EstimatedRemainingCost = graph.EstimatedDistance(neighborNode, destNode),
                            PreviousPiece = currentPiece,
                        };
                        openSet.Add(neighborNode, neighborPiece);
                    }
                    else
                    {
                        if (costToNeighbor < neighborPiece.CostFromStart)
                        {
                            if (inClosedSet)
                            {
                                closedSet.Remove(neighborNode);
                                openSet.Add(neighborNode, neighborPiece);
                            }
                            neighborPiece.CostFromStart = costToNeighbor;
                            neighborPiece.PreviousPiece = currentPiece;
                        }
                    }
                }

                if (currentPiece.Node.Equals(destNode))
                    destPiece = currentPiece;
            }

            List<T> path = null;
            if (destPiece != null)
            {
                PathPiece<T> cursor = destPiece;
                path = new List<T>();
                while (!cursor.Node.Equals(startNode))
                {
                    path.Add(cursor.Node);
                    cursor = cursor.PreviousPiece;
                }

                path.Reverse();
            }

            // TODO: replace with proper logging
            Console.WriteLine($"Path searched between {startNode} and {destNode}: closedSetCount={closedSet.Count}; timeMS={timer.ElapsedMilliseconds}");

            return path;
        }

        private class PathPiece<T>
        {
            public T Node { get; }
            public double CostFromStart { get; set; }
            public double EstimatedRemainingCost { get; set; }
            public PathPiece<T> PreviousPiece { get; set; }

            public PathPiece(T node)
            {
                this.Node = node;
            }
        }
    }
}
