using System;

namespace BattlePlanEngine.Dto.V3
{
    public class UnitCharacteristics
    {
        public string Name { get; set; }
        public char Symbol { get; set; } = '?';
        public string Behavior { get; set; } = "None";
        public bool CanBeAttacker { get; set; }
        public bool CanBeDefender { get; set; }
        public bool Attackable { get; set; } = true;
        public bool BlocksTile { get; set; } = true;

        public double SpeedTilesPerSec { get; set; }
        public int InitialHitPoints { get; set; }
        public string WeaponType { get; set; }
        public double WeaponUseTime { get; set; }
        public double WeaponReloadTime { get; set; }
        public double WeaponRangeTiles { get; set; }
        public int WeaponDamage { get; set; }
        public int ResourceCost { get; set; }
        public double CrowdAversionBias { get; set; }
        public double HurtAversionBias { get; set; }
    }
}
