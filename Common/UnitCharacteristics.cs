using System;
using BattlePlan.Common;

namespace BattlePlan.Common
{
    public class UnitCharacteristics
    {
        public string Name { get; set; }
        public char Symbol { get; set; } = '?';
        public UnitBehavior Behavior { get; set; } = UnitBehavior.None;
        public bool CanAttack { get; set; }
        public bool CanDefend { get; set; }
        public double SpeedTilesPerSec { get; set; }
        public int InitialHitPoints { get; set; }
        public double WeaponUseTime { get; set; }
        public double WeaponReloadTime { get; set; }
        public double WeaponRangeTiles { get; set; }
        public int WeaponDamage { get; set; }
        public int ResourceCost { get; set; }
        public double CrowdAversionBias { get; set; }
        public double HurtAversionBias { get; set; }
    }
}
