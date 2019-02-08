using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Scenario
    {
        public Terrain Terrain { get; set; }
        public IList<AttackPlan> AttackPlans { get; set; } = new List<AttackPlan>();
        public IList<DefensePlan> DefensePlans { get; set; } = new List<DefensePlan>();
        public IList<DefenderChallenge> Challenges { get; set; } = new List<DefenderChallenge>();
    }
}
