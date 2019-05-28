using System;
using System.Collections.Generic;
using BattlePlanEngine.Model;

namespace BattlePlanConsole.Generators
{
    /// <summary>
    /// Parameters for random AttackPlan creation.
    /// </summary>
    public class AttackPlanGeneratorOptions
    {
        public IList<string> FodderUnitNames = new string[] { "Zombie", "Grunt" };
        public IList<string> EliteUnitNames = new string[] { "Berserker", "Crossbowman", "Storm-Mage" };
        public IList<string> LonerUnitNames = new string[] { "Scout" };

        public IList<int> CostTiers = new int[] { 200, 400, 900 };
        public IList<double> EliteProbTiers = new double[] { 0.0, 0.60, 0.85 };
        public int NumberOfEliteTypesPerPlan = 1;
        public int MinimumTimeBetweenSpawnGroups = 12;
        public int InterGroupTime = 4;

        public int TotalResourceBudget = 2000;
    }
}
