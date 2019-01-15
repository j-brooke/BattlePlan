using System;

namespace BattlePlan.Common
{
    public class AttackerSpawn
    {
        public double Time { get; set; }
        public string UnitType { get; set; }

        public int SpawnPointIndex { get; set; }
        public AttackerSpawn()
        {
        }

        public AttackerSpawn(AttackerSpawn other)
        {
            this.Time = other.Time;
            this.UnitType = other.UnitType;
            this.SpawnPointIndex = other.SpawnPointIndex;
        }
    }
}
