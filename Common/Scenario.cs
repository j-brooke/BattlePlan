using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Scenario
    {
        // TODO: Find a better way to handle this.  It keeps getting me in trouble.
        public const int MaxTeamId = 4;

        public Terrain Terrain { get; set; }
        public IList<AttackPlan> AttackPlans { get; set; } = new List<AttackPlan>();
        public IList<DefensePlan> DefensePlans { get; set; } = new List<DefensePlan>();
    }
}