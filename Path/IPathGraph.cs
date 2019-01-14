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
        IEnumerable<T> Neighbors(T fromNode);
        double Cost(T fromNode, T toNode);
        double EstimatedDistance(T fromNode, T toNode);
    }
}
