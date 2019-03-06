using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that tracks how much potential damage is directed at any given tile, per team.
    /// For example, if a unit can attack into a tile (it's in range and has line of sight),
    /// that tile's hurt factor is equal to that unit's DPS (or more if other units can also
    /// attack into it.)
    /// </summary>
    internal class HurtMap
    {
        public HurtMap(Terrain terrain, UnitCharacteristics fireClass)
        {
            _terrain = terrain;
            _fireClass = fireClass;
            _fireDPS = (fireClass==null)? 0.0 : fireClass.WeaponDamage /  (fireClass.WeaponUseTime + fireClass.WeaponReloadTime);
        }

        /// <summary>
        /// Returns the DPS directed at the given position by defenders who are not on
        /// the given team.
        /// </summary>
        public double GetHurtFactor(Vector2Di pos, int teamId)
        {
            return GetHurtFactor(pos.X, pos.Y, teamId);
        }

        /// <summary>
        /// Returns the DPS directed at the given position by defenders who are not on
        /// the given team.
        /// </summary>
        public double GetHurtFactor(short posX, short posY, int teamId)
        {
            var totalHurt = 0.0;
            foreach (var keyValPair in _layers)
            {
                if (keyValPair.Key!=teamId)
                    totalHurt += keyValPair.Value[posX,posY];
            }
            return totalHurt;
        }

        /// <summary>
        /// Records that something has changed for the given team, and it needs to be rebuilt.
        /// </summary>
        public void InvalidateTeam(int teamId)
        {
            _layerIsValid[teamId] = false;
            _logger.Debug("Invalidating HurtMap for team {0}", teamId);
        }

        /// <summary>
        /// Rebuilds the internal table for any teams marked as invalid.
        /// </summary>
        public void Update(IEnumerable<BattleEntity> entities)
        {
            for (var teamId=0; teamId<=Scenario.MaxTeamId; ++teamId)
            {
                bool isValid = false;
                _layerIsValid.TryGetValue(teamId, out isValid);
                if (!isValid)
                    UpdateForTeam(entities, teamId);
            }
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private Terrain _terrain;
        private UnitCharacteristics _fireClass;
        private double _fireDPS;

        private Dictionary<int, bool> _layerIsValid = new Dictionary<int, bool>();
        private Dictionary<int, double[,]> _layers = new Dictionary<int, double[,]>();

        private void UpdateForTeam(IEnumerable<BattleEntity> entities, int teamId)
        {
            var teamDefenders = entities
                .Where( (ent) => ent.TeamId==teamId && !ent.IsAttacker && ent.Class.WeaponType!=WeaponType.None);
            var layer = new double[_terrain.Width, _terrain.Height];

            _logger.Debug("Rebuilding HurtMap for team {0} from {1} units", teamId, teamDefenders.Count());

            foreach (var unit in teamDefenders)
            {
                var range = unit.Class.WeaponRangeTiles;
                var minX = Math.Max(0, (int)Math.Round(unit.Position.X-range));
                var minY = Math.Max(0, (int)Math.Round(unit.Position.Y-range));
                var maxX = Math.Min(_terrain.Width-1, (int)Math.Round(unit.Position.X+range));
                var maxY = Math.Min(_terrain.Height-1, (int)Math.Round(unit.Position.Y+range));

                double thisUnitsDPS;
                switch (unit.Class.WeaponType)
                {
                    case WeaponType.Physical:
                        thisUnitsDPS = unit.Class.WeaponDamage / (unit.Class.WeaponUseTime + unit.Class.WeaponReloadTime);
                        break;
                    case WeaponType.Flamestrike:
                        // Weapon damage in this case is the fire's time-to-live in 1/100ths of a second.
                        thisUnitsDPS = _fireDPS * unit.Class.WeaponDamage / 100.0 / (unit.Class.WeaponUseTime + unit.Class.WeaponReloadTime);
                        break;
                    case WeaponType.ChainLightning:
                        // Bonus perceived threat because it hits so many at a time.  And it's freaking lightning.
                        thisUnitsDPS = 2.0 * unit.Class.WeaponDamage / (unit.Class.WeaponUseTime + unit.Class.WeaponReloadTime);
                        break;
                    default:
                        throw new NotImplementedException("Weapon type not implemented");
                }

                for (int y=minY; y<=maxY; ++y)
                {
                    for (int x=minX; x<=maxX; ++x)
                    {
                        var evalPos = new Vector2Di(x,y);
                        if (unit.Position.DistanceTo(evalPos) > range)
                            continue;

                        if (!_terrain.HasLineOfSight(unit.Position, evalPos))
                            continue;

                        layer[x,y] += thisUnitsDPS;
                    }
                }
            }

            _layers[teamId] = layer;
            _layerIsValid[teamId] = true;
        }
    }
}
