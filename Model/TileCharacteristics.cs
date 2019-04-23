using System;

namespace BattlePlan.Model
{
    /// <summary>
    /// Properties of a spot on a map.
    /// </summary>
    public class TileCharacteristics
    {
        public bool BlocksMovement { get; set; }
        public bool BlocksVision { get; set; }
        public char Appearance { get; set; }
        public string Name { get; set; }
    }
}