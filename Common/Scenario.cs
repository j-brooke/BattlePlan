using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Scenario
    {
        public Terrain Terrain { get; set; }
        public IList<AttackPlan> AttackPlans { get; set; }
        public IList<DefensePlan> DefensePlans { get; set; }
    }
}