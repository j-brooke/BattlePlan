using System;
using System.Collections.Generic;

namespace BattlePlanEngine.Dto.V3
{
    public class Scenario
    {
        /// <summary>
        /// Optional text shown at the top of the screen for tutorials or flavor.
        /// </summary>
        public IList<string> BannerText { get; set; } = new List<string>();

        public Terrain Terrain { get; set; }
        public IList<AttackPlan> AttackPlans { get; set; } = new List<AttackPlan>();
        public IList<DefensePlan> DefensePlans { get; set; } = new List<DefensePlan>();
        public IList<DefenderChallenge> Challenges { get; set; } = new List<DefenderChallenge>();
    }
}
