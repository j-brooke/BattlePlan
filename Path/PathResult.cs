using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Path
{
    /// <summary>
    /// Bundle of information returned after searching for a path.
    /// </summary>
    public class PathResult<T>
    {
        public List<T> Path { get; set; }
        public double PathCost { get; set; }
        public long SolutionTimeMS { get; set; }
    }
}
