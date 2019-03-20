using System;
using BattlePlan.Generators;

namespace BattlePlan.Generators
{
    public class GeneratorOptions
    {
        public MapGeneratorOptions MapGeneratorOptions { get; set; } = new MapGeneratorOptions();
        public AttackPlanGeneratorOptions AttackPlanGeneratorOptions { get; set; } = new AttackPlanGeneratorOptions();
    }
}
