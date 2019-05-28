using System;

namespace BattlePlanConsole.Generators
{
    public class GeneratorOptions
    {
        public MapGeneratorOptions MapGeneratorOptions { get; set; } = new MapGeneratorOptions();
        public AttackPlanGeneratorOptions AttackPlanGeneratorOptions { get; set; } = new AttackPlanGeneratorOptions();
    }
}
