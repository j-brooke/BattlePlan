using System;

namespace BattlePlan.Model
{
    public class TileCharacteristics
    {
        public bool BlocksMovement { get; set; }
        public bool BlocksVision { get; set; }
        public char Appearance { get; set; }
        public string Name { get; set; }
    }
}