using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class DefensePlan
    {
        public int TeamId { get; set; }
        public IList<DefenderPlacement> Placements { get; set; }
    }
}