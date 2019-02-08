using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class DefenderChallenge
    {
        public string Name { get; set; }
        public int PlayerTeamId { get; set; }

        public int MinimumDistFromSpawnPts { get; set; }

        public int MinimumDistFromGoalPts { get; set; }

        public int MaximumResourceCost { get; set; }

        public int MaximumTotalUnitCount { get; set; }
        public int MaximumDefendersLostCount { get; set; }
        public bool AttackersMustNotReachGoal { get; set; }

        public IDictionary<string,int> MaximumUnitTypeCount { get; set; } = new Dictionary<string,int>();
    }
}
