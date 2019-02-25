using System;
using System.Collections.Generic;

namespace BattlePlan.Path
{
    /// <summary>
    /// Interface for answering questions about some concrete space in terms the pathfinding algorithm
    /// understands.  Use this to convert a tile map into a weighted directed graph, for instance.
    /// </summary>
    /// <typeparam name="TNode">Type that identifies distinct nodes or locations.  (For example, Vector2D, string, etc.)</typeparam>
    /// <typeparam name="TPassthrough">Type of data passed through from PathSolver.FindPath to the IPathGraph.Cost and EstimatedCost.)</typeparam>
    public interface IPathGraph<TNode, TPassthrough>
    {
        /// <summary>
        /// Returns an enumeration of nodes that are reachable in one step from the given one.
        /// </summary>
        IEnumerable<TNode> Neighbors(TNode fromNode);

        /// <summary>
        /// Returns the actual cost of moving from one node to one of its neihbors.  This could be
        /// a distance, time, monetary value, or whatever.  The cost must be non-negative.
        /// </summary>
        double Cost(TNode fromNode, TNode toNode, TPassthrough callerData);

        /// <summary>
        /// Estimated cost for the entire path from one node to any other one (not necessarily a neighbor).
        /// This is what the A* algorithm often calls the "heuristic".
        /// </summary>
        double EstimatedCost(TNode fromNode, TNode toNode, TPassthrough callerData);
    }
}
