using System;

namespace BattlePlan.Dto.V2
{
    public class TileCharacteristics
    {
        public bool BlocksMovement { get; set; }
        public bool BlocksVision { get; set; }
        public char Appearance { get; set; }
        public string Name { get; set; }
    }
}