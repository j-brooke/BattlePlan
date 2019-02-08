using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class BattleResolution
    {
        public Terrain Terrain { get; set; }
        public IList<UnitCharacteristics> UnitTypes { get; set; }
        public IList<BattleEvent> Events { get; set; }

        /// <summary>
        /// Key value pairs where the key is the TeamID of an attacking team and the value
        /// is the number of attackers who reached their goal.
        /// </summary>
        public IDictionary<int,int> AttackerBreachCounts { get; set; }

        /// <summary>
        /// Key value pairs where the key is the TeamID of a defending team and the value
        /// is the number of defenders who died.
        /// </summary>
        public IDictionary<int,int> DefenderCasualtyCounts { get; set; }

        public IList<DefenderChallenge> ChallengesAchieved { get; set; }
    }
}
