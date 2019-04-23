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

        /// <summary>
        /// Can these units be attacked by enemies?  Fire is an example where this is false.
        /// </summary>
        public bool Attackable { get; set; } = true;

        /// <summary>
        /// Can other units move into this unit's tile?  Fire is an example where this is false.
        /// </summary>
        public bool BlocksTile { get; set; } = true;
        public double SpeedTilesPerSec { get; set; }
        public int InitialHitPoints { get; set; }
        public WeaponType WeaponType { get; set; } = WeaponType.None;

        /// <summary>
        /// How long it takes from the decision to attack to when the damage is delivered.  Units can't move
        /// while attacking.
        /// </summary>
        public double WeaponUseTime { get; set; }

        /// <summary>
        /// How long after using a weapon before it can be used again.  This counts down while the unit is doing
        /// other things, like moving.
        /// </summary>
        public double WeaponReloadTime { get; set; }
        public double WeaponRangeTiles { get; set; }
        public int WeaponDamage { get; set; }

        /// <summary>
        /// Relative value of the unit.  Used for score and scenario balancing.
        /// </summary>
        public int ResourceCost { get; set; }

        /// <summary>
        /// Number represeenting how much this unit type tries to space out away from nearby friendly units.
        /// When zero, they'll all follow the same line.
        /// </summary>
        public double CrowdAversionBias { get; set; }

        /// <summary>
        /// Number representing how far out of its way this unit will go to avoid taking damage.  When zero,
        /// units don't care at all that they're marching into an enemy archer kill field.
        /// </summary>
        public double HurtAversionBias { get; set; }
    }
}
