using System;

namespace BattlePlan.Model
{
    /// <summary>
    /// Characteristics of different unit classes.
    /// </summary>
    public class UnitCharacteristics
    {
        public string Name { get; set; }
        public char Symbol { get; set; } = '?';
        public UnitBehavior Behavior { get; set; } = UnitBehavior.None;
        public bool CanBeAttacker { get; set; }
        public bool CanBeDefender { get; set; }
        public bool Attackable { get; set; } = true;
        public bool BlocksTile { get; set; } = true;
        public double SpeedTilesPerSec { get; set; }
        public int InitialHitPoints { get; set; }
        public WeaponType WeaponType { get; set; } = WeaponType.None;
        public double WeaponUseTime { get; set; }
        public double WeaponReloadTime { get; set; }
        public double WeaponRangeTiles { get; set; }
        public int WeaponDamage { get; set; }
        public int ResourceCost { get; set; }
        public double CrowdAversionBias { get; set; }
        public double HurtAversionBias { get; set; }
    }
}
