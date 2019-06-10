using System;

namespace BattlePlanEngine.Dto.V3
{
    public class AttackerSpawn
    {
        public double Time { get; set; }
        public string UnitType { get; set; }

        public int SpawnPointIndex { get; set; }
    }
}
