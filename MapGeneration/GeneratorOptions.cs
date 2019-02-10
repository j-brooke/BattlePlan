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
        /// <summary>
        /// Total map height in tiles.
        /// </summary>
        public int Height { get; set; } = 22;

        /// <summary>
        /// Total map width in tiles.
        /// </summary>
        public int Width { get; set; } = 50;

        /// <summary>
        /// Maximum width of random rectangles (for stones, water, etc).  If the map's width is larger than its
        /// height, you probably want the chunk sizes to be wider than they are high as well.
        /// </summary>
        public int ChunkSizeX { get; set; } = 7;

        /// <summary>
        /// Maximum height of random rectangles applied to the map.
        /// </summary>
        public int ChunkSizeY{ get; set; } = 4;

        /// <summary>
        /// Number of random rectangles of water created on the map.  This happens before stones, so some may be
        /// overwritten.
        /// </summary>
        public int WaterRectCount { get; set; } = 4;

        /// <summary>
        /// Number of tiles by which existing water regions (from the rectangles above) can grow.
        /// </summary>
        public int WaterDotCount { get; set; } = 30;

        /// <summary>
        /// Number of random stone rectangles to apply to the map.
        /// </summary>
        public int StoneRectCount { get; set; } = 60;

        /// <summary>
        /// Number of tiles by which existing stone regions (from the rectangles above) can grow.
        /// </summary>
        public int StoneDotCount { get; set; } = 200;

        /// <summary>
        /// Number of open rectangles applied at once, causing already placed stones/water to be removed.
        /// This is repeatedly applied until all spawn/goal points are reachable from each other.
        /// </summary>
        public int OpenRectCount { get; set; } = 10;

        /// <summary>
        /// Probability (0-100) that fog will be added to the map.  (Sometimes it's covered up even when
        /// generated.)
        /// </summary>
        public int FogChancePercent { get; set; } = 50;

        /// <summary>
        /// Number of spawn points created for team 1.
        /// </summary>
        public int SpawnPointCount { get; set; } = 1;

        /// <summary>
        /// Number of goal points created for team 1.
        /// </summary>
        public int GoalCount { get; set; } = 1;
    }
}