using System;
using System.Collections.Generic;

namespace BattlePlan.Model
{
    /// <summary>
    /// A goal for a player to meet.  This only applies to players creating DefensePlans.
    /// </summary>
    public class DefenderChallenge
    {
        public string Name { get; set; }
        public int PlayerTeamId { get; set; }

        public int? MinimumDistFromSpawnPts { get; set; }

        public int? MinimumDistFromGoalPts { get; set; }

        public int? MaximumResourceCost { get; set; }

        public int? MaximumTotalUnitCount { get; set; }
        public int? MaximumDefendersLostCount { get; set; }
        public bool AttackersMustNotReachGoal { get; set; }

        public IDictionary<string,int> MaximumUnitTypeCount { get; set; } = new Dictionary<string,int>();

        public DefenderChallenge Clone()
        {
            var copy = (DefenderChallenge)this.MemberwiseClone();
            copy.MaximumUnitTypeCount = (this.MaximumUnitTypeCount!=null)?
                new Dictionary<string,int>(this.MaximumUnitTypeCount)
                : new Dictionary<string,int>();
            return copy;
        }
    }
}
