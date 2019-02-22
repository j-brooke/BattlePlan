using System;

namespace BattlePlan.Dto.V1
{
    public class TileCharacteristics
    {
        public bool BlocksMovement { get; set; }
        public bool BlocksVision { get; set; }
        public string Appearance { get; set; }
        public string Name { get; set; }
    }
}