using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class BattleResolution
    {
        public Terrain Terrain { get; set; }
        public IList<BattleEvent> Events { get; set; }

        /// <summary>
        /// Key value pairs where the key is the TeamID of an attacking team and the value
        /// is the number of attackers who reached their goal.
        /// </summary>
        public IDictionary<int,int> AttackerBreachCounts { get; set; }
    }
}
