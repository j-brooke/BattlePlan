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
            _globalTimer.Start();

            var openPriorityQueue = new IntrinsicPriorityQueue<PathPiece<T>>(512, LeastTotalEstimatedDistance);
            var allPieces = new Dictionary<T,PathPiece<T>>();
            PathPiece<T> destPiece = null;

            var startPiece = new PathPiece<T>(startNode)
            {
                EstimatedRemainingCost = graph.EstimatedDistance(startNode, destNode),
                IsOpen = true
            };
            openPriorityQueue.Enqueue(startPiece);
            allPieces.Add(startPiece.Node, startPiece);
            _enqueueCount += 1;

            while (openPriorityQueue.Count>0 && destPiece==null)
            {
                _maxQueueSize = Math.Max(_maxQueueSize, openPriorityQueue.Count);

                var currentPiece = openPriorityQueue.Dequeue();
                currentPiece.IsOpen = false;
                _dequeueCount += 1;

                var neighbors = graph.Neighbors(currentPiece.Node);
                foreach (var neighborNode in neighbors)
                {
                    PathPiece<T> neighborPiece = null;
                    allPieces.TryGetValue(neighborNode, out neighborPiece);

                    double costToNeighbor = currentPiece.CostFromStart + graph.Cost(currentPiece.Node, neighborNode);
                    if (neighborPiece == null)
                    {
                        neighborPiece = new PathPiece<T>(neighborNode)
                        {
                            CostFromStart = costToNeighbor,
                            EstimatedRemainingCost = graph.EstimatedDistance(neighborNode, destNode),
                            PreviousPiece = currentPiece,
                            IsOpen = true,
                        };
                        openPriorityQueue.Enqueue(neighborPiece);
                        allPieces.Add(neighborPiece.Node, neighborPiece);
                        _enqueueCount += 1;
                    }
                    else
                    {
                        if (costToNeighbor < neighborPiece.CostFromStart)
                        {
                            neighborPiece.CostFromStart = costToNeighbor;
                            neighborPiece.PreviousPiece = currentPiece;
                            if (neighborPiece.IsOpen)
                            {
                                openPriorityQueue.AdjustPriority(neighborPiece);
                                _removeCount += 1;
                            }
                            else
                            {
                                neighborPiece.IsOpen = true;
                                openPriorityQueue.Enqueue(neighborPiece);
                                _enqueueCount += 1;
                            }
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

            _globalTimer.Stop();

            _logger.Trace("Path searched from {0} to {1}: visitedNodeCount={2}; timeMS={3}",
                startNode,
                destNode,
                allPieces.Count,
                timer.ElapsedMilliseconds);

            return path;
        }

        public static string DebugInfo()
        {
            return $"timeMS={_globalTimer.ElapsedMilliseconds}; enqueueCount={_enqueueCount}; dequeueCount={_dequeueCount}; removeCount={_removeCount}; maxSize={_maxQueueSize}";
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly System.Diagnostics.Stopwatch _globalTimer = new System.Diagnostics.Stopwatch();
        private static int _enqueueCount = 0;
        private static int _dequeueCount = 0;
        private static int _removeCount = 0;
        private static int _maxQueueSize = 0;

        private static bool LeastTotalEstimatedDistance<T>(PathPiece<T> a, PathPiece<T> b)
        {
            return (a.CostFromStart + a.EstimatedRemainingCost) < (b.CostFromStart + b.EstimatedRemainingCost);
        }

        private class PathPiece<T>
        {
            public T Node { get; }
            public double CostFromStart { get; set; }
            public double EstimatedRemainingCost { get; set; }
            public PathPiece<T> PreviousPiece { get; set; }
            public bool IsOpen { get; set; }

            public PathPiece(T node)
            {
                this.Node = node;
            }
        }
    }
}
