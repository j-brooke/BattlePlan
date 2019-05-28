using System;
using System.Collections.Generic;

namespace BattlePlanEngine.Dto.V2
{
    public class DefensePlan
    {
        public int TeamId { get; set; }
        public IList<DefenderPlacement> Placements { get; set; } = new List<DefenderPlacement>();
    }
}