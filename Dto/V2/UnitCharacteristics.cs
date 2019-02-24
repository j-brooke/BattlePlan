using System;

namespace BattlePlan.Dto.V2
{
    public class UnitCharacteristics
    {
        public string Name { get; set; }
        public char Symbol { get; set; } = '?';
        public string Behavior { get; set; } = "None";
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
