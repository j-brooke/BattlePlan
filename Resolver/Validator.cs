using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    public static class Validator
    {
        public static IEnumerable<string> FindTerrainErrors(Terrain terrain)
        {
            if (terrain.Width<0)
                yield return "Terrain width is negative";
            else if (terrain.Height>_maxSaneWidth)
                yield return "Terrain width is too large";

            if (terrain.Height<0)
                yield return "Terrain height is negative";
            else if (terrain.Height>_maxSaneHeight)
                yield return "Terrain height is too large";

            if (terrain.TileTypes == null || !terrain.TileTypes.Any())
            {
                yield return "No terrain tile types are defined";
                yield break;
            }

            if (terrain.Width<0 || terrain.Width>_maxSaneWidth || terrain.Height<0 || terrain.Height>_maxSaneHeight)
                yield break;

            var tileVals = terrain.Tiles?.SelectMany( (row) => row ?? Array.Empty<byte>() ) ?? Enumerable.Empty<byte>();
            var hasTileOutOfBounds = tileVals.Any( (val) => val<0 || val>terrain.TileTypes.Count );
            if (hasTileOutOfBounds)
            {
                yield return "Some tile values are undefined";
                yield break;
            }

            var allSpawns = terrain.SpawnPointsMap?.Values.SelectMany( (ptsForTeam) => ptsForTeam ) ?? Enumerable.Empty<Vector2Di>();
            var noSpawnPoints = !allSpawns.Any();
            if (noSpawnPoints)
                yield return "No spawn points are defined";

            var hasSpawnOutOfBounds = allSpawns.Any( (spPt) => spPt.X<0 || spPt.Y<0 || spPt.X>=terrain.Width | spPt.Y>=terrain.Height );
            if (hasSpawnOutOfBounds)
                yield return "Some spawn points are outside of the map's boundaries";

            var hasBlockedSpawn = allSpawns.Any( (spPt) => terrain.GetTile(spPt).BlocksMovement );
            if (hasBlockedSpawn)
                yield return "Some spawn points are on impassable terrain";

            var allGoals = terrain.GoalPointsMap?.Values.SelectMany( (ptsForTeam) => ptsForTeam ) ?? Enumerable.Empty<Vector2Di>();
            var noGoalPoints = !allGoals.Any();
            if (noGoalPoints)
                yield return "No goal points are defined";

            var hasGoalOutOfBounds = allGoals.Any( (gPt) => gPt.X<0 || gPt.Y<0 || gPt.X>=terrain.Width | gPt.Y>=terrain.Height );
            if (hasGoalOutOfBounds)
                yield return "Some goal points are outside of the map's boundaries";

            var hasBlockedGoal = allGoals.Any( (gPt) => terrain.GetTile(gPt).BlocksMovement );
            if (hasBlockedGoal)
                yield return "Some goal points are on impassable terrain";

            if (noSpawnPoints || hasSpawnOutOfBounds || hasBlockedSpawn || noGoalPoints || hasGoalOutOfBounds || hasBlockedGoal)
                yield break;

            foreach (var teamId in terrain.SpawnPointsMap.Keys)
            {
                foreach (var spawnPt in terrain.SpawnPointsMap[teamId])
                {
                    var reachableLocs = MovementModel.FindReachableLocations(terrain, spawnPt);
                    var someGoalIsReachable = terrain.GoalPointsMap.ContainsKey(teamId)
                        && terrain.GoalPointsMap[teamId].Any( (gPt) => reachableLocs.Contains(gPt) );
                    if (!someGoalIsReachable)
                        yield return $"No goal point is reachable from team {teamId}'s spawn point at {spawnPt}";
                }
            }
        }

        public static IEnumerable<string> FindAttackPlanErrors(Terrain terrain,
            IEnumerable<UnitCharacteristics> unitTypes,
            IEnumerable<AttackPlan> attackPlans)
        {
            var unitTypeMap = MakeUnitTypeMap(unitTypes);
            if (unitTypeMap.Count==0)
                yield return "No unit types defined";

            foreach (var plan in attackPlans)
            {
                if (plan.Spawns==null || plan.Spawns.Count==0)
                    continue;
                if (!terrain.SpawnPointsMap.ContainsKey(plan.TeamId))
                {
                    yield return $"Attackers exist for team {plan.TeamId} but no spawn points.";
                }
                else
                {
                    var spawnPoints = terrain.SpawnPointsMap[plan.TeamId];
                    var hasBadSpawnPoints = plan.Spawns.Any( (aSp) => aSp.SpawnPointIndex<0 || aSp.SpawnPointIndex>=spawnPoints.Count );
                    if (hasBadSpawnPoints)
                        yield return "Spawn point index out of bounds";

                    var hasBadSpawnTimes = plan.Spawns.Any( (aSp) => aSp.Time > _maxSaneSpawnTime );
                    if (hasBadSpawnTimes)
                        yield return "Spawn time too large";

                    var hasInvalidType = plan.Spawns.Any( (aSp) => !unitTypeMap.ContainsKey(aSp.UnitType) || !unitTypeMap[aSp.UnitType].CanAttack );
                    if (hasInvalidType)
                        yield return "Some attacker unit types are invalid";
                }
            }
        }

        public static IEnumerable<string> FindDefensePlanErrors(Terrain terrain,
            IEnumerable<UnitCharacteristics> unitTypes,
            IEnumerable<DefensePlan> defensePlans)
        {
            var unitTypeMap = MakeUnitTypeMap(unitTypes);
            if (unitTypeMap.Count==0)
                yield return "No unit types defined";

            var allPlacements = defensePlans?.SelectMany( (plan) => plan.Placements ) ?? Enumerable.Empty<DefenderPlacement>();

            var hasInvalidType = allPlacements.Any( (dp) => !unitTypeMap.ContainsKey(dp.UnitType) || !unitTypeMap[dp.UnitType].CanDefend );
            if (hasInvalidType)
                yield return "Some defender unit types are invalid";

            var hasOutOfBounds = allPlacements.Any( (dp) => dp.Position.X<0 || dp.Position.Y<0 || dp.Position.X >= terrain.Width || dp.Position.Y>=terrain.Height);
            if (hasOutOfBounds)
                yield return "Some defenders are placed outside of the map's boundaries";

            var hasBlockedDefs = allPlacements.Any( (dp) => terrain.GetTile(dp.Position).BlocksMovement );
            if (hasBlockedDefs)
                yield return "Some defenders are placed on impassable terrain";
        }

        private const int _maxSaneWidth = 1000;
        private const int _maxSaneHeight = 1000;
        private const double _maxSaneSpawnTime = 300.0;

        private static Dictionary<string,UnitCharacteristics> MakeUnitTypeMap(IEnumerable<UnitCharacteristics> unitTypeList)
        {
            if (unitTypeList == null)
                return new Dictionary<string,UnitCharacteristics>();
            var unitKVPs = unitTypeList.Select( (ut) => new KeyValuePair<string,UnitCharacteristics>(ut.Name, ut) );
            return new Dictionary<string,UnitCharacteristics>(unitKVPs);
        }
    }
}