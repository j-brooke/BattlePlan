using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Path
{
    public class PathSolver<T>
    {
        public PathSolver(IPathGraph<T> worldGraph)
        {
            _worldGraph = worldGraph;
            _infoGraph = new Dictionary<T, NodeInfo>();
            _seqNum = 0;
            _timer = new System.Diagnostics.Stopwatch();
        }

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
                    .ToList();
            }

            _totalSolutionTimeMS += _timer.ElapsedMilliseconds;
        }

        public void ClearAdjacencyGraph()
        {
            // Break apart the dense network of objects pointing at each other to make it easier for the
            // garbage collector.
            foreach (var info in _infoGraph.Values)
                info.Neighbors?.Clear();

            _infoGraph.Clear();
        }


        public PathResult<T> FindPath(T startNodeId, IEnumerable<T> endNodeIdList)
        {
            _timer.Restart();

            // Choose a new sequence number.  We'll rely on this for lazy initialization.
            _seqNum += 1;
            var openListQueue = new IntrinsicPriorityQueue<NodeInfo>(PriorityFunction);

            if (!endNodeIdList.Any())
                throw new PathfindingException("endNodeIdList is empty");

            foreach (var endNodeId in endNodeIdList)
            {
                NodeInfo endInfo;
                if (!_infoGraph.TryGetValue(endNodeId, out endInfo))
                    throw new PathfindingException("Ending node is not in the adjacency graph.");
                endInfo.IsDestinationForSeqNum = _seqNum;
            }

            NodeInfo startInfo;
            if (!_infoGraph.TryGetValue(startNodeId, out startInfo))
                throw new PathfindingException("Starting node is not in the adjacency graph.");
            startInfo.LastVisitedSeqNum = _seqNum;
            startInfo.CostFromStart = 0;
            startInfo.PreviousPiece = null;
            startInfo.EstimatedRemainingCost = EstimateRemainingCostToAny(startNodeId, endNodeIdList);
            startInfo.IsOpen = true;
            openListQueue.Enqueue(startInfo);
            _enqueueCount += 1;

            NodeInfo foundDestInfo = null;
            while (openListQueue.Count>0)
            {
                _maxQueueSize = Math.Max(_maxQueueSize, openListQueue.Count);
                var currentInfo = openListQueue.Dequeue();
                _dequeueCount += 1;

                currentInfo.IsOpen = false;
                if (currentInfo.IsDestinationForSeqNum==_seqNum)
                {
                    foundDestInfo = currentInfo;
                    break;
                }

                foreach (var neighborInfo in currentInfo.Neighbors)
                {
                    double costToNeighbor = currentInfo.CostFromStart + _worldGraph.Cost(currentInfo.NodeId, neighborInfo.NodeId);
                    if (neighborInfo.LastVisitedSeqNum!=_seqNum)
                    {
                        neighborInfo.LastVisitedSeqNum = _seqNum;
                        neighborInfo.CostFromStart = costToNeighbor;
                        neighborInfo.PreviousPiece = currentInfo;
                        neighborInfo.EstimatedRemainingCost = EstimateRemainingCostToAny(neighborInfo.NodeId, endNodeIdList);
                        neighborInfo.IsOpen = true;

                        openListQueue.Enqueue(neighborInfo);
                        _enqueueCount += 1;
                    }
                    else
                    {
                        if (costToNeighbor < neighborInfo.CostFromStart)
                        {
                            neighborInfo.CostFromStart = costToNeighbor;
                            neighborInfo.PreviousPiece = currentInfo;
                            if (neighborInfo.IsOpen)
                            {
                                openListQueue.AdjustPriority(neighborInfo);
                                _adjustCount += 1;
                            }
                            else
                            {
                                neighborInfo.IsOpen = true;
                                openListQueue.Enqueue(neighborInfo);
                                _enqueueCount += 1;
                            }
                        }
                    }
                }
            }

            // If we reached a destination, put together a list of the NodeIds that make up the path we found.
            List<T> path = null;
            if (foundDestInfo != null)
            {
                path = new List<T>();
                NodeInfo iter = foundDestInfo;
                while (iter.PreviousPiece != null)
                {
                    path.Add(iter.NodeId);
                    iter = iter.PreviousPiece;
                }

                path.Reverse();
            }

            long elapsedTimeMS = _timer.ElapsedMilliseconds;
            _totalSolutionTimeMS += elapsedTimeMS;

            return new PathResult<T>()
            {
                Path = path,
                PathCost = foundDestInfo?.CostFromStart ?? 0,
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
        private int _seqNum;
        private int _enqueueCount;
        private int _dequeueCount;
        private int _adjustCount;
        private int _maxQueueSize;
        private long _totalSolutionTimeMS;

        private double EstimateRemainingCostToAny(T startNodeId, IEnumerable<T> endNodeIdList)
        {
            return endNodeIdList
                .Select( (endNodeId) => _worldGraph.EstimatedDistance(startNodeId,endNodeId) )
                .Min();
        }

        private static bool PriorityFunction(NodeInfo a, NodeInfo b)
        {
            return (a.CostFromStart + a.EstimatedRemainingCost) < (b.CostFromStart + b.EstimatedRemainingCost);
        }

        private class NodeInfo
        {
            public T NodeId { get; }
            public List<NodeInfo> Neighbors { get; set; }
            public double CostFromStart { get; set; }
            public double EstimatedRemainingCost { get; set; }
            public NodeInfo PreviousPiece { get; set; }
            public bool IsOpen { get; set; }
            public int IsDestinationForSeqNum { get; set; }

            public int LastVisitedSeqNum { get; set; }

            public NodeInfo(T nodeId)
            {
                this.NodeId = nodeId;
            }
        }
    }
}