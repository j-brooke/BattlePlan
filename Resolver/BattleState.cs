using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that resolves a scenario (map, attacker and defender lists, etc) into a BattleResolution.
    /// It tracks the state of everything while the resolution is going on and can be queried by
    /// the other pieces.
    /// </summary>
    public sealed class BattleState
    {
        public BattleResolution Resolve(Scenario scenario, UnitTypeMap unitTypes)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            _terrain = scenario.Terrain;
            _attackPlans = new List<AttackPlan>(scenario.AttackPlans);
            _defensePlans = new List<DefensePlan>(scenario.DefensePlans ?? Enumerable.Empty<DefensePlan>());

            _unitTypeMap = unitTypes;

            var fireClass = _unitTypeMap.Get("Fire");

            // Validate and exit early if we fail.
            var valErrs = GetValidationFailures(scenario, unitTypes);
            if (valErrs.Any())
            {
                return new BattleResolution()
                {
                    Terrain = scenario.Terrain,
                    UnitTypes = unitTypes.AsList,
                    ErrorMessages = valErrs,
                };
            }

            _events = new List<BattleEvent>();
            _entities = new List<BattleEntity>();
            _blockedPositions = new BattleEntity[_terrain.Width,_terrain.Height];
            _pathGraph = new BattlePathGraph(this);
            _hurtMap = new HurtMap(_terrain, fireClass);
            _miscSpawnQueue = new List<SpawnRequest>();

            // Make a bunch of queues for the attackers remaining to be spawned.
            _remainingAttackerSpawns = new SpawnQueueCluster(_terrain, _attackPlans);

            var attackerBreachCounts = new Dictionary<int,int>();
            foreach (var teamId in _remainingAttackerSpawns.AttackerTeamIds)
                attackerBreachCounts[teamId] = 0;

            var defenderCasualtyCounts = new Dictionary<int,int>();
            foreach (var plan in _defensePlans)
                defenderCasualtyCounts[plan.TeamId] = 0;

            // Spawn all defenders
            foreach (var plan in _defensePlans)
            {
                foreach (var placement in plan.Placements)
                {
                    var id = GenerateId(0.0, placement.UnitType);
                    var classChar = _unitTypeMap.Get(placement.UnitType);
                    var newEntity = new BattleEntity(id, classChar, plan.TeamId, false);
                    newEntity.Spawn(this, placement.Position);
                    _entities.Add(newEntity);
                    AddEvent(CreateEvent(0.0, BattleEventType.Spawn, newEntity, null));
                }
            }

            // Update the hurtmap with the initial defenders list.
            _hurtMap.Update(_entities);

            var previousTime = 0.0;
            var time = 0.0;
            var battleEnded = false;

            while (!battleEnded)
            {
                previousTime = time;
                time += _timeSlice;

                // Make a list of all hazard tiles such as fire.
                _hazardLocations = _entities.Where( (ent) => !ent.Class.BlocksTile && !ent.Class.Attackable )
                    .Select( (ent) => ent.Position )
                    .ToList();

                // Update existing entities
                var entitiesReachingGoal = new List<BattleEntity>();
                foreach (var entity in _entities)
                {
                    var newEvents = entity.Update(this, time, _timeSlice);
                    for (int i=0; newEvents!=null && i<newEvents.Length; ++i)
                    {
                        AddEvent(newEvents[i]);

                        // If the entity moved, check to see if it reached its goal and take note.
                        if (newEvents[i].Type==BattleEventType.EndMovement)
                        {
                            if (GetGoalTiles(entity.TeamId).Contains(entity.Position))
                                entitiesReachingGoal.Add(entity);
                        }
                    }
                }

                // Remove any entities that just reached their goals.
                // Note: should this be before or after checking for death?
                foreach (var winEnt in entitiesReachingGoal)
                {
                    AddEvent(CreateEvent(time, BattleEventType.ReachesGoal, winEnt, null));
                    AddEvent(CreateEvent(time, BattleEventType.Despawn, winEnt, null));
                    winEnt.PrepareToDespawn(this);
                    _entities.Remove(winEnt);

                    attackerBreachCounts[winEnt.TeamId] += 1;
                }

                // Remove entities that just died
                var deadEntities = _entities.Where( (ent) => ent.HitPoints<=0 );
                _entities = _entities.Except(deadEntities).ToList();
                foreach (var deadEnt in deadEntities)
                {
                    AddEvent(CreateEvent(time, BattleEventType.Die, deadEnt, null));
                    AddEvent(CreateEvent(time, BattleEventType.Despawn, deadEnt, null));
                    deadEnt.PrepareToDespawn(this);
                    _hurtMap.InvalidateTeam(deadEnt.TeamId);

                    if (!deadEnt.IsAttacker)
                        defenderCasualtyCounts[deadEnt.TeamId] += 1;
                }

                // Remove entities whose time-to-live has expired.
                var expriedEntities = _entities.Where( (ent) => ent.TimeToLive <= 0).ToList();
                foreach (var expEnt in expriedEntities)
                {
                    AddEvent(CreateEvent(time, BattleEventType.Despawn, expEnt, null));
                    expEnt.PrepareToDespawn(this);
                    _hurtMap.InvalidateTeam(expEnt.TeamId);
                    _entities.Remove(expEnt);
                }

                // Spawn new entities
                SpawnAttackers(time);
                AddMiscellaneousSpawns(time);

                // Update the hurtmap.
                _hurtMap.Update(_entities);

                // End things if there are no attacker units left (and nothing left to spawn),
                // or if the time gets too high.
                var noMobileUnitsLeft = false;
                var spawnListsAreEmpty = _remainingAttackerSpawns.Count == 0;
                if (spawnListsAreEmpty)
                {
                    noMobileUnitsLeft = !_entities.Any( (ent) => ent.IsAttacker );
                }

                battleEnded = (time >= _maxTime)
                    || noMobileUnitsLeft;
            }

            var resolution = new BattleResolution()
            {
                BannerText = new List<string>(scenario.BannerText ?? Enumerable.Empty<string>() ),
                Terrain = _terrain,
                UnitTypes = _unitTypeMap.AsList,
                Events = _events,
                AttackerBreachCounts = attackerBreachCounts,
                DefenderCasualtyCounts = defenderCasualtyCounts,
                ChallengesAchieved = new List<DefenderChallenge>(),
                ChallengesFailed = new List<DefenderChallenge>(),
            };

            ResolveChallenges(scenario, resolution, unitTypes);
            CountResources(scenario, resolution);

            _logger.Info("Pathfinding stats: " + _pathGraph.DebugInfo() );
            _logger.Info("Resolution stats: timeMS={0}; eventCount={1}", timer.ElapsedMilliseconds, _events.Count);

            return resolution;
        }

        internal Terrain Terrain => _terrain;
        internal HurtMap HurtMap => _hurtMap;
        internal BattlePathGraph PathGraph => _pathGraph;

        internal BattleEntity GetEntityBlockingTile(Vector2Di position)
        {
            return _blockedPositions[position.X, position.Y];
        }

        internal BattleEntity GetEntityById(string id)
        {
            return _entities.FirstOrDefault( (ent) => ent.Id==id );
        }

        internal List<BattleEntity> GetAllEntities()
        {
            return _entities;
        }

        internal void SetEntityBlockingTile(Vector2Di position, BattleEntity entity)
        {
            _blockedPositions[position.X, position.Y] = entity;
        }

        internal void ClearEntityBlockingTile(Vector2Di position)
        {
            _blockedPositions[position.X, position.Y] = null;
        }

        internal IList<Vector2Di> GetGoalTiles(int teamId)
        {
            return this._terrain.GoalPointsMap[teamId];
        }

        internal void RequestSpawn(string unitType, int teamId, bool isAttacker, Vector2Di pos, double? timeToLive)
        {
            _miscSpawnQueue.Add(new SpawnRequest()
            {
                UnitType = unitType,
                TeamId = teamId,
                Position = pos,
                TimeToLive = timeToLive,
                IsAttacker = isAttacker,
            });
        }

        internal IList<Vector2Di> GetHazardLocations()
        {
            return _hazardLocations;
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private Terrain _terrain;
        private List<AttackPlan> _attackPlans;
        private List<DefensePlan> _defensePlans;

        private List<BattleEvent> _events;
        private List<BattleEntity> _entities;

        private BattleEntity[,] _blockedPositions;

        private BattlePathGraph _pathGraph;

        private UnitTypeMap _unitTypeMap;

        private int _nextId;
        private HurtMap _hurtMap;
        private SpawnQueueCluster _remainingAttackerSpawns;
        private List<SpawnRequest> _miscSpawnQueue;
        private IList<Vector2Di> _hazardLocations;

        private static readonly double _maxTime = 300.0;
        private static readonly double _timeSlice = 0.1;

        private static BattleEvent CreateEvent(double time, BattleEventType type, BattleEntity sourceEnt, BattleEntity targetEnt)
        {
            var evt = new BattleEvent()
            {
                Time = time,
                Type = type,
                SourceEntity = sourceEnt?.Id,
                SourceLocation = sourceEnt?.Position,
                SourceTeamId = sourceEnt?.TeamId ?? 0,
                SourceClass = sourceEnt?.Class.Name,
                TargetEntity = targetEnt?.Id,
                TargetLocation = targetEnt?.Position,
                TargetTeamId = targetEnt?.TeamId ?? 0,
            };
            return evt;
        }

        private void AddEvent(BattleEvent evt)
        {
            _events.Add(evt);

            if (_logger.IsDebugEnabled)
                _logger.Debug(evt.ToString());
        }

        private void SpawnAttackers(double time)
        {
            // Loop through each spawn point for each attacking team.
            foreach (var teamId in _remainingAttackerSpawns.AttackerTeamIds)
            {
                var spawnsPtsForTeam = _terrain.SpawnPointsMap[teamId];
                for (int spawnPtIdx=0; spawnPtIdx<spawnsPtsForTeam.Count; ++spawnPtIdx)
                {
                    var pos = spawnsPtsForTeam[spawnPtIdx];

                    // Skip this spawn point for now if there's something already on that tile.
                    if (GetEntityBlockingTile(pos) != null)
                        continue;

                    // Get the next spawn command for this team and spawnPoint, if any, as long as its
                    // spawn time is <= the current time.
                    var spawnDef = _remainingAttackerSpawns.GetNext(teamId, spawnPtIdx, time);
                    if (spawnDef != null)
                    {
                        var id = GenerateId(time, spawnDef.UnitType);
                        var classChar = _unitTypeMap.Get(spawnDef.UnitType);
                        var newEntity = new BattleEntity(id, classChar, teamId, true);
                        newEntity.Spawn(this, pos);

                        _entities.Add(newEntity);
                        AddEvent(CreateEvent(time, BattleEventType.Spawn, newEntity, null));
                    }
                }
            }
        }

        private void AddMiscellaneousSpawns(double time)
        {
            foreach (var spawnReq in _miscSpawnQueue)
            {
                var id = GenerateId(time, spawnReq.UnitType);
                var classChar = _unitTypeMap.Get(spawnReq.UnitType);
                var newEntity = new BattleEntity(id, classChar, spawnReq.TeamId, spawnReq.IsAttacker);

                if (spawnReq.TimeToLive.HasValue)
                    newEntity.TimeToLive = spawnReq.TimeToLive.Value;

                newEntity.Spawn(this, spawnReq.Position);

                _entities.Add(newEntity);
                AddEvent(CreateEvent(time, BattleEventType.Spawn, newEntity, null));

                _hurtMap.InvalidateTeam(spawnReq.TeamId);
            }
            _miscSpawnQueue.Clear();
        }

        internal string GenerateId(double time, string cls)
        {
            var id = $"{cls}{_nextId.ToString()}";
            _nextId += 1;
            return id;
        }

        private IList<string> GetValidationFailures(Scenario scenario, UnitTypeMap unitTypes)
        {
            var errs = new List<string>();
            errs.AddRange(Validator.FindTerrainErrors(scenario.Terrain));

            if (errs.Count==0)
            {
                errs.AddRange(Validator.FindAttackPlanErrors(scenario.Terrain, unitTypes, scenario.AttackPlans));
                errs.AddRange(Validator.FindDefensePlanErrors(scenario.Terrain, unitTypes, scenario.DefensePlans));
            }

            return errs;
        }

        private void ResolveChallenges(Scenario scenario, BattleResolution resolution, UnitTypeMap unitTypes)
        {
            if (scenario.Challenges != null)
            {
                foreach (var chal in scenario.Challenges)
                {
                    // Validate each challenge against the setup and results.  Put them into the appropriate
                    // buckets in the BattleResolution.  Log if necessary.
                    var failures = Validator.GetChallengeDisqualifiers(scenario, resolution, unitTypes, chal);
                    if (failures.Any())
                    {
                        resolution.ChallengesFailed.Add(chal);
                        var buff = new System.Text.StringBuilder($"Challenge '{chal.Name}' failed.");
                        foreach (var msg in failures)
                            buff.AppendLine().Append("    ").Append(msg);
                        _logger.Debug(buff.ToString());
                    }
                    else
                    {
                        resolution.ChallengesAchieved.Add(chal);
                        _logger.Debug($"Challenge '{chal.Name}' achieved.");
                    }
                }
            }
        }

        /// <summary>
        /// Adds up the resource cost used by each team for attacking and defending, and populates
        /// the resolution.
        /// </summary>
        private void CountResources(Scenario scenario, BattleResolution resolution)
        {
            if (scenario.DefensePlans != null)
            {
                var defenderMap = new Dictionary<int,int>();

                foreach (var plan in scenario.DefensePlans)
                {
                    if (plan.Placements==null)
                        continue;

                    var countForPlan = 0;
                    foreach (var placement in plan.Placements)
                        countForPlan += _unitTypeMap.Get(placement.UnitType).ResourceCost;

                    if (defenderMap.ContainsKey(plan.TeamId))
                        defenderMap[plan.TeamId] += countForPlan;
                    else
                        defenderMap[plan.TeamId] = countForPlan;
                }

                resolution.DefenderResourceTotals = defenderMap;
            }

            if (scenario.AttackPlans != null)
            {
                var attackerMap = new Dictionary<int,int>();

                foreach (var plan in scenario.AttackPlans)
                {
                    if (plan.Spawns==null)
                        continue;

                    var countForPlan = 0;
                    foreach (var spawn in plan.Spawns)
                        countForPlan += _unitTypeMap.Get(spawn.UnitType).ResourceCost;

                    if (attackerMap.ContainsKey(plan.TeamId))
                        attackerMap[plan.TeamId] += countForPlan;
                    else
                        attackerMap[plan.TeamId] = countForPlan;
                }

                resolution.AttackerResourceTotals = attackerMap;
            }
        }

        private class SpawnRequest
        {
            public string UnitType { get; set; }
            public int TeamId { get; set; }
            public bool IsAttacker { get; set; }
            public Vector2Di Position { get; set; }
            public double? TimeToLive { get; set; }
        }
    }
}