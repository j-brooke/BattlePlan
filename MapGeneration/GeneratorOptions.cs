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
        public int Height { get; set; }
        public int Width { get; set; }
        public int ChunkSizeX { get; set; }
        public int ChunkSizeY{ get; set; }
        public int PositiveCycleCount { get; set; }
        public int NegativeCycleCount { get; set; }
        public int SpawnPointCount { get; set; }
        public int GoalCount { get; set; }
    }
}