using System;
using System.Collections.Generic;
using BattlePlan.Common;

namespace BattlePlan.MapGeneration
{
    /// <summary>
    /// Parameters for random map generation.
    /// </summary>
    public class GeneratorOptions
    {
        public int Height { get; set; } = 50;
        public int Width { get; set; } = 36;
        public int ChunkSizeX { get; set; } = 7;
        public int ChunkSizeY{ get; set; } = 4;
        public int PositiveCycleCount { get; set; } = 60;
        public int NegativeCycleCount { get; set; } = 10;
        public int SpawnPointCount { get; set; } = 3;
        public int GoalCount { get; set; } = 3;
    }
}