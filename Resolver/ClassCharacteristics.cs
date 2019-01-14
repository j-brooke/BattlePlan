using System;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    internal class ClassCharacteristics
    {
        public UnitClass Class { get; private set; }
        public double SpeedTilesPerSec { get; private set; }
        public int InitialHitPoints { get; private set; }
        public double WeaponUseTime { get; private set; }
        public double WeaponReloadTime { get; private set; }
        public double WeaponRangeTiles { get; private set; }
        public int WeaponDamage { get; private set; }

        public static ClassCharacteristics Get(UnitClass cls)
        {
            switch (cls)
            {
                case Common.UnitClass.AttackerGrunt:
                    return AttackerGrunt;
                case Common.UnitClass.DefenderArcher:
                    return DefenderArcher;
                default:
                    throw new NotImplementedException();
            }
        }

        private static ClassCharacteristics AttackerGrunt = new ClassCharacteristics()
        {
            Class = UnitClass.AttackerGrunt,
            SpeedTilesPerSec = 1.0,
            InitialHitPoints = 100,
            WeaponUseTime = 0.5,
            WeaponReloadTime = 0.5,
            WeaponRangeTiles = 1.5,
            WeaponDamage = 60,
        };

        private static ClassCharacteristics DefenderArcher = new ClassCharacteristics()
        {
            Class = UnitClass.DefenderArcher,
            SpeedTilesPerSec = 0,
            InitialHitPoints = 75,
            WeaponUseTime = 0.3,
            WeaponReloadTime = 0.7,
            WeaponRangeTiles = 10.0,
            WeaponDamage = 20,
        };
    }
}