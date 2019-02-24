using System;

namespace BattlePlan.Dto.V2
{
    public class AttackerSpawn
    {
        public double Time { get; set; }
        public string UnitType { get; set; }

        public int SpawnPointIndex { get; set; }
    }
}
