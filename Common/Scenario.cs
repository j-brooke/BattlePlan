using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Scenario
    {
        public Terrain Terrain { get; set; }
        public IList<UnitCharacteristics> UnitTypes { get; set; }
        public IList<AttackPlan> AttackPlans { get; set; } = new List<AttackPlan>();
        public IList<DefensePlan> DefensePlans { get; set; } = new List<DefensePlan>();
    }
}