using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Path
{
    /// <summary>
    /// Class for finding the shortest path between arbitrary nodes in a directed graph
    /// using the A* algorithm.
    /// </summary>
    /// <typeparam name="T">Type that identifies distinct nodes or locations.  (For example, Vector2D, string, etc.)</typeparam>
    /// <remarks>
    /// <para>This class is designed for calculating many paths using the same adjacency graph each time, from a single thread.
    /// If your terrain frequently changes or which nodes are considered adjacent to others varies from one case to another,
    /// this probably isn't optimal for you.</para>
    /// <para>On construction you give PathSolver a IPathGraph instance.  This is an object that knows how to describe
    /// your problem space to the PathSolver.  In particular, it knows: 1) Which nodes can travel to others in a single step
    /// (neighbors); 2) The actual cost of travelling from one node to another (in distance, time, whatever); and
    /// 3) An estimated cost for travelling from one node to a distant one (the A* heuristic).</para>
    /// <para>Before calling FindPath, you'll need to call BuildAdjacencyGraph.  This builds internal data structures
    /// that will be used on all future FindPath calls.</para>
    /// </remarks>
    public class PathSolver<T>
    {
        public PathSolver(IPathGraph<T> worldGraph)
        {
            _worldGraph = worldGraph;
            _infoGraph = new Dictionary<T, NodeInfo>();
            _seqNum = 0;
            _timer = new System.Diagnostics.Stopwatch();
        }

        /// <summary>
        /// Prepares the PathSolver by building a graph of which nodes are connected directly to each other.
        /// Call this before calling FindPath, or if the shape of your world changes.
        /// </summary>
        public void BuildAdjacencyGraph(IEnumerable<T> seedNodeIds)
        {
            _timer.Restart();
            _infoGraph.Clear();

            // Create a NodeInfo for every reachable NodeId from the given start ones.
            var queue = new Queue<T>(seedNodeIds);
            while (queue.Count>0)
            {
                var nodeId = queue.Dequeue();
                if (!_infoGraph.ContainsKey(nodeId))
                {
                    var info = new NodeInfo(nodeId);
                    _infoGraph.Add(info.NodeId, info);

                    foreach (var neighbor in _worldGraph.Neighbors(nodeId))
                        queue.Enqueue(neighbor);
                }
            }

            // Now go back and make a list of neighbor NodeInfos for each NodeInfo so we don't have to
            // go through a lookup table every time.
            foreach (var info in _infoGraph.Values)
            {
                info.Neighbors = _worldGraph.Neighbors(info.NodeId)
                    .Select( (nid) => _infoGraph[nid] )
                    .ToArray();
            }

            _totalSolutionTimeMS += _timer.ElapsedMilliseconds;
        }

        /// <summary>
        /// Clears the internal data structure.  Doing this might make it easier for the garbage collector
        /// to reclaim memory.
        /// </summary>
        public void ClearAdjacencyGraph()
        {
            // Break apart the dense network of objects pointing at each other to make it easier for the
            // garbage collector.
            foreach (var info in _infoGraph.Values)
                info.Neighbors = null;

            _infoGraph.Clear();
        }

        /// <summary>
        /// Finds the shortest path from the given start point to one of the given end points.
        /// (If your IPathGraph.EstimatedCost can overestimate the cost, then this won't always
        /// strictly be the shortest path.)
        /// </summary>
        public PathResult<T> FindPath(T startNodeId, IEnumerable<T> endNodeIdList)
        {
            _timer.Restart();

            // Choose a new sequence number.  We'll rely on this to know which NodeInfos
            // have been touched aleady in this pass, and which contain stale data from
            // previous calls.
            _seqNum += 1;

            // The openQueue contains all of the nodes that have been discovered but haven't
            // been fully processed yet.
            var openQueue = new IndexedIntrinsicPriorityQueue<NodeInfo>(PriorityFunction);

            // Make sure our end points exist in the pre-build graph, and mark them as end points.
            if (!endNodeIdList.Any())
                throw new PathfindingException("endNodeIdList is empty");

            foreach (var endNodeId in endNodeIdList)
            {
                NodeInfo endInfo;
                if (!_infoGraph.TryGetValue(endNodeId, out endInfo))
                    throw new PathfindingException("Ending node is not in the adjacency graph.");
                endInfo.IsDestinationForSeqNum = _seqNum;
            }

            // Init the starting node, put it in the openQueue, and go.
            NodeInfo startInfo;
            if (!_infoGraph.TryGetValue(startNodeId, out startInfo))
                throw new PathfindingException("Starting node is not in the adjacency graph.");
            startInfo.LastVisitedSeqNum = _seqNum;
            startInfo.BestCostFromStart = 0;
            startInfo.BestPreviousNode = null;
            startInfo.EstimatedRemainingCost = EstimateRemainingCostToAny(startNodeId, endNodeIdList);
            startInfo.IsOpen = true;
            openQueue.Enqueue(startInfo);
            _enqueueCount += 1;

            NodeInfo arrivalInfo = null;
            while (openQueue.Count>0)
            {
                // Pull the current item from the queue.
                _maxQueueSize = Math.Max(_maxQueueSize, openQueue.Count);
                var currentInfo = openQueue.Dequeue();
                currentInfo.IsOpen = false;
                _dequeueCount += 1;

                // If this node is our goal, stop the loop.
                if (currentInfo.IsDestinationForSeqNum==_seqNum)
                {
                    arrivalInfo = currentInfo;
                    break;
                }

                foreach (var neighborInfo in currentInfo.Neighbors)
                {
                    double costToNeighbor = currentInfo.BestCostFromStart + _worldGraph.Cost(currentInfo.NodeId, neighborInfo.NodeId);

                    // If the neighbor node hasn't been touched on this FindPath call, re-initialize it and put it
                    // in openQueue for later consideration.
                    if (neighborInfo.LastVisitedSeqNum!=_seqNum)
                    {
                        neighborInfo.LastVisitedSeqNum = _seqNum;
                        neighborInfo.BestCostFromStart = costToNeighbor;
                        neighborInfo.BestPreviousNode = currentInfo;
                        neighborInfo.EstimatedRemainingCost = EstimateRemainingCostToAny(neighborInfo.NodeId, endNodeIdList);
                        neighborInfo.IsOpen = true;

                        openQueue.Enqueue(neighborInfo);
                        _enqueueCount += 1;
                    }
                    else
                    {
                        // We've already looked at the neighbor node, but it's possible that coming at it from the
                        // current node is more efficient.  If so, update it.
                        if (costToNeighbor < neighborInfo.BestCostFromStart)
                        {
                            neighborInfo.BestCostFromStart = costToNeighbor;
                            neighborInfo.BestPreviousNode = currentInfo;
                            if (neighborInfo.IsOpen)
                            {
                                openQueue.AdjustPriority(neighborInfo);
                                _adjustCount += 1;
                            }
                            else
                            {
                                neighborInfo.IsOpen = true;
                                openQueue.Enqueue(neighborInfo);
                                _enqueueCount += 1;
                            }
                        }
                    }
                }
            }

            // If we reached a destination, put together a list of the NodeIds that make up the path we found.
            List<T> path = null;
            if (arrivalInfo != null)
            {
                path = new List<T>();
                NodeInfo iter = arrivalInfo;
                while (iter.BestPreviousNode != null)
                {
                    path.Add(iter.NodeId);
                    iter = iter.BestPreviousNode;
                }

                path.Reverse();
            }

            long elapsedTimeMS = _timer.ElapsedMilliseconds;
            _totalSolutionTimeMS += elapsedTimeMS;

            return new PathResult<T>()
            {
                Path = path,
                PathCost = arrivalInfo?.BestCostFromStart ?? 0,
                SolutionTimeMS = elapsedTimeMS,
            };
        }

        public string DebugInfo()
        {
            return $"timeMS={_totalSolutionTimeMS}; enqueueCount={_enqueueCount}; dequeueCount={_dequeueCount}; adjustCount={_adjustCount}; maxSize={_maxQueueSize}";
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly System.Diagnostics.Stopwatch _timer;

        private readonly IPathGraph<T> _worldGraph;
        private readonly Dictionary<T,NodeInfo> _infoGraph;

        // Each time we run FindPath we use a new _seqNum.  This helps us keep track of which NodeInfo
        // instances we've touched this call, and which ones have stale data from previous calls.
        private int _seqNum;
        private int _enqueueCount;
        private int _dequeueCount;
        private int _adjustCount;
        private int _maxQueueSize;
        private long _totalSolutionTimeMS;

        /// <summary>
        /// Returns the smallest of the estimated costs to the given end points.
        /// </summary>
        private double EstimateRemainingCostToAny(T startNodeId, IEnumerable<T> endNodeIdList)
        {
            return endNodeIdList
                .Select( (endNodeId) => _worldGraph.EstimatedCost(startNodeId,endNodeId) )
                .Min();
        }

        private static bool PriorityFunction(NodeInfo a, NodeInfo b)
        {
            return (a.BestCostFromStart + a.EstimatedRemainingCost) < (b.BestCostFromStart + b.EstimatedRemainingCost);
        }

        /// <summary>
        /// Private class holding info during path-finding.
        /// </summary>
        private class NodeInfo : IndexedQueueItem
        {
            /// <summary>Identifier for a node.  Could be a string, Vector2D, etc.</summary>
            public T NodeId { get; }

            /// <summary>Link to all NodeInfos reachable from here in one step.</summary>
            public NodeInfo[] Neighbors { get; set; }

            /// <summary>Best total cost of all path steps to get here that we've found so far.</summary>
            public double BestCostFromStart { get; set; }

            /// <summary>Heuristic guess of how far we are from the goal.  Used to prioritize next examined nodes.</summary>
            public double EstimatedRemainingCost { get; set; }

            /// <summary>The node that we came from that gave us our curren BestCostFromStart.</summary>
            public NodeInfo BestPreviousNode { get; set; }

            /// <summary>Is this NodeInfo in openQueue?  It's quicker if we track it here than ask the queue.</summary>
            public bool IsOpen { get; set; }

            /// <summary>Is this one of our final destination points?</summary>
            public int IsDestinationForSeqNum { get; set; }

            /// <summary>
            /// Sequence number of the last FindPath call in which we touched this NodeInfo.  If it's not equal
            /// to the current call's seqnum we need to reinit a bunch of stuff.  This is quicker than looping through
            /// all known NodeInfos at the start of a FindPath and initializing stuff, or constructing brand new
            /// NodeInfos and wiring up their Neighbors.
            /// </summary>
            public int LastVisitedSeqNum { get; set; }

            public NodeInfo(T nodeId)
            {
                this.NodeId = nodeId;
                this.QueueIndex = -1;
            }
        }
    }
}
