using System;
using System.Collections.Generic;

namespace BattlePlan.Path
{
    /// <summary>
    /// Interface for answering questions about some concrete space in terms the pathfinding algorithm
    /// understands.  Use this to convert a tile map into a weighted directed graph, for instance.
    /// </summary>
    public interface IPathGraph<T>
    {
        /// <summary>
        /// Returns a collection of nodes that are directly connected to the given one.
        /// </summary>
        IEnumerable<T> Neighbors(T fromNode);

        /// <summary>
        /// Returns the actual cost of moving from one node to one of its neihbors.  This could be
        /// a distance, time, monetary value, or whatever.  The cost must be non-negative.
        /// </summary>
        double Cost(T fromNode, T toNode);

        /// <summary>
        /// Estimated cost for the entire path from one node to any other one (not necessarily a neighbor).
        /// This is what the A* algorithm often calls the "heuristic".
        /// </summary>
        double EstimatedDistance(T fromNode, T toNode);
    }
}
