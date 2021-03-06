using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlanEngine.Model;

namespace BattlePlanEngine.Resolver
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
            UnitTypeMap unitTypes,
            IEnumerable<AttackPlan> attackPlans)
        {
            var unitTypeMap = unitTypes;
            if (unitTypeMap.AsList.Count==0)
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

                    var hasInvalidType = plan.Spawns.Any( (aSp) => unitTypeMap.Get(aSp.UnitType)==null || !unitTypeMap.Get(aSp.UnitType).CanBeAttacker );
                    if (hasInvalidType)
                        yield return "Some attacker unit types are invalid";
                }
            }
        }

        public static IEnumerable<string> FindDefensePlanErrors(Terrain terrain,
            UnitTypeMap unitTypes,
            IEnumerable<DefensePlan> defensePlans)
        {
            var unitTypeMap = unitTypes;
            if (unitTypeMap.AsList.Count==0)
                yield return "No unit types defined";

            var allPlacements = defensePlans?.SelectMany( (plan) => plan.Placements ) ?? Enumerable.Empty<DefenderPlacement>();

            var hasInvalidType = allPlacements.Any( (dp) => unitTypeMap.Get(dp.UnitType)==null || !unitTypeMap.Get(dp.UnitType).CanBeDefender );
            if (hasInvalidType)
                yield return "Some defender unit types are invalid";

            var hasOutOfBounds = allPlacements.Any( (dp) => dp.Position.X<0 || dp.Position.Y<0 || dp.Position.X >= terrain.Width || dp.Position.Y>=terrain.Height);
            if (hasOutOfBounds)
                yield return "Some defenders are placed outside of the map's boundaries";

            var hasBlockedDefs = allPlacements.Any( (dp) => terrain.GetTile(dp.Position).BlocksMovement );
            if (hasBlockedDefs)
                yield return "Some defenders are placed on impassable terrain";

            // Placing any unit on a spawn point will keep anything from spawning.  Kinda cheating.
            var allPlacementLocs = allPlacements.Select( (placement) => placement.Position );
            var allSpawnLocs = terrain.SpawnPointsMap.Values.SelectMany( (list) => list );
            if (allPlacementLocs.Intersect(allSpawnLocs).Any())
                yield return "Some defenders are placed on spawn points.";

            // Placing an enemy on a goal is okay, since they can be killed.  But placing a friendly on a goal
            // will forever keep the attackers out, so that's bad.
            bool blockingSameTeamGoal = false;
            foreach (var plan in defensePlans)
            {
                IList<Vector2Di> goalsForTeam = null;
                if (terrain.GoalPointsMap != null && terrain.GoalPointsMap.TryGetValue(plan.TeamId, out goalsForTeam))
                {
                    var teamPlacementLocs = plan.Placements.Select( (placement) => placement.Position );
                    if (goalsForTeam.Intersect(teamPlacementLocs).Any())
                    {
                        blockingSameTeamGoal = true;
                        break;
                    }
                }
            }
            if (blockingSameTeamGoal)
                yield return "Some defenders are placed on their own team's goals";
        }

        public static IEnumerable<string> GetChallengeDisqualifiers(Scenario scenario,
            BattleResolution resolution,
            UnitTypeMap unitTypes,
            DefenderChallenge challenge)
        {
            var unitTypeMap = unitTypes;

            var totalResourceCost = 0;
            var totalUnitCount = 0;
            var unitTypeCounts = new Dictionary<string,int>();

            var allEnemySpawns = scenario.Terrain.SpawnPointsMap
                .Where( (kvp) => kvp.Key!=challenge.PlayerTeamId )
                .SelectMany( (kvp) => kvp.Value )
                .ToList();
            var allEnemyGoals = scenario.Terrain.GoalPointsMap
                .Where( (kvp) => kvp.Key!=challenge.PlayerTeamId )
                .SelectMany( (kvp) => kvp.Value )
                .ToList();

            bool tooCloseToSpawns = false;
            bool tooCloseToGoals = false;
            foreach (var plan in scenario.DefensePlans)
            {
                if (plan.TeamId != challenge.PlayerTeamId || plan.Placements==null)
                    continue;

                foreach (var placement in plan.Placements)
                {
                    totalUnitCount += 1;
                    totalResourceCost += unitTypeMap.Get(placement.UnitType).ResourceCost;

                    int unitCount = 0;
                    unitTypeCounts.TryGetValue(placement.UnitType, out unitCount);
                    unitTypeCounts[placement.UnitType] = unitCount + 1;

                    if (challenge.MinimumDistFromSpawnPts.HasValue)
                    {
                        if (allEnemySpawns.Any( (pt) => pt.DistanceTo(placement.Position) < challenge.MinimumDistFromSpawnPts.Value ))
                            tooCloseToSpawns = true;
                    }

                    if (challenge.MinimumDistFromGoalPts.HasValue)
                    {
                        if (allEnemyGoals.Any( (pt) => pt.DistanceTo(placement.Position) < challenge.MinimumDistFromGoalPts.Value ))
                            tooCloseToGoals = true;
                    }
                }
            }

            if (tooCloseToSpawns)
                yield return $"Some defenders are closer than {challenge.MinimumDistFromSpawnPts.Value} tiles from enemy spawn points.";
            if (tooCloseToGoals)
                yield return $"Some defenders are closer than {challenge.MinimumDistFromGoalPts.Value} tiles from enemy goals.";

            if (challenge.MaximumTotalUnitCount.HasValue && totalUnitCount>challenge.MaximumTotalUnitCount.Value)
                yield return $"More than {challenge.MaximumTotalUnitCount.Value} defenders placed.";

            if (challenge.MaximumResourceCost.HasValue && totalResourceCost>challenge.MaximumResourceCost.Value)
                yield return $"More than {challenge.MaximumResourceCost.Value} resource points spent.";

            var maxUnitTypeCountList = challenge.MaximumUnitTypeCount ?? Enumerable.Empty<KeyValuePair<string,int>>();
            foreach (var kvp in maxUnitTypeCountList)
            {
                int actualCount = 0;
                unitTypeCounts.TryGetValue(kvp.Key, out actualCount);
                if (actualCount > kvp.Value)
                    yield return $"More than {kvp.Value} defenders of type {kvp.Key} placed.";
            }

            if (challenge.AttackersMustNotReachGoal && (resolution?.AttackerBreachCounts != null))
            {
                int totalBreachCount = 0;
                foreach (var kvp in resolution.AttackerBreachCounts)
                {
                    if (kvp.Key != challenge.PlayerTeamId && kvp.Value>0)
                        totalBreachCount += kvp.Value;
                }
                if (totalBreachCount > 0)
                    yield return $"{totalBreachCount} attackers reached their goals";
            }

            int defenderCasualties = 0;
            if (resolution?.DefenderCasualtyCounts.ContainsKey(challenge.PlayerTeamId) ?? false)
                defenderCasualties = resolution.DefenderCasualtyCounts[challenge.PlayerTeamId];
            if (challenge.MaximumDefendersLostCount.HasValue && defenderCasualties > challenge.MaximumDefendersLostCount)
                yield return $"More than {challenge.MaximumDefendersLostCount.Value} defenders died.";
        }

        public static IEnumerable<string> GetChallengeRequirements(DefenderChallenge challenge)
        {
            if (challenge.AttackersMustNotReachGoal)
                yield return "No attackers reach their goals.";
            if (challenge.MinimumDistFromSpawnPts.HasValue)
                yield return $"No defenders closer than {challenge.MinimumDistFromSpawnPts.Value} tiles from enemy spawn points.";
            if (challenge.MinimumDistFromGoalPts.HasValue)
                yield return $"No defenders closer than {challenge.MinimumDistFromGoalPts.Value} tiles from enemy goals.";
            if (challenge.MaximumResourceCost.HasValue)
                yield return $"No more than {challenge.MaximumResourceCost.Value} resource points spent.";
            if (challenge.MaximumTotalUnitCount.HasValue)
                yield return $"No more than {challenge.MaximumTotalUnitCount.Value} defenders placed.";
            if (challenge.MaximumDefendersLostCount.HasValue)
                yield return $"No more than {challenge.MaximumDefendersLostCount.Value} defenders may die.";

            var unitTypes = challenge.MaximumUnitTypeCount.Keys.OrderBy( (str) => str );
            foreach (var unit in unitTypes)
                yield return $"No more than {challenge.MaximumUnitTypeCount[unit]} defenders of type {unit} placed.";
        }

        private const int _maxSaneWidth = 1000;
        private const int _maxSaneHeight = 1000;
        private const double _maxSaneSpawnTime = 300.0;

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
    }
}