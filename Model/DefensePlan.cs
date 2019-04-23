using System;
using System.Collections.Generic;

namespace BattlePlan.Model
{
    /// <summary>
    /// Collection of all of a particular team's defender placements.
    /// </summary>
    public class DefensePlan
    {
        public int TeamId { get; set; }
        public IList<DefenderPlacement> Placements { get; set; } = new List<DefenderPlacement>();
    }
}