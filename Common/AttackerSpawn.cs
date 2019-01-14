using System;

namespace BattlePlan.Common
{
    public class AttackerSpawn
    {
        public double Time { get; set; }
        public UnitClass Class { get; set; }

        public int SpawnPointIndex { get; set; }
        public AttackerSpawn()
        {
        }

        public AttackerSpawn(AttackerSpawn other)
        {
            this.Time = other.Time;
            this.Class = other.Class;
            this.SpawnPointIndex = other.SpawnPointIndex;
        }
    }
}
