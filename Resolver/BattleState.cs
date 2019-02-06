using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that resolves a scenario (map, attacker and defender lists, etc) into a BattleResolution.
    /// It tracks the state of everything while the resolution is going on and can be queried by
    /// the other pieces.
    /// </summary>
    public sealed class BattleState
    {
        public Terrain Terrain => _terrain;
        public BattleResolution Resolve(Scenario scenario, IList<UnitCharacteristics> unitTypes)
        {
            _runTimer = System.Diagnostics.Stopwatch.StartNew();

            _terrain = scenario.Terrain;
            _attackPlans = new List<AttackPlan>(scenario.AttackPlans);
            _defensePlans = new List<DefensePlan>(scenario.DefensePlans ?? Enumerable.Empty<DefensePlan>());

            // Make a lookup table for unit types.
            _unitTypeMap = new Dictionary<string, UnitCharacteristics>();
            foreach (var unitType in unitTypes)
                _unitTypeMap.Add(unitType.Name, unitType);

            // TODO: validate
            // * terrain is passable from spawn to goal
            // * attack and defense plans use only legal units
            // * defenders placed in legal locations
            // * attack spawns are in legal places

            _events = new List<BattleEvent>();
            _entities = new List<BattleEntity>();
            _blockedPositions = new BattleEntity[_terrain.Width,_terrain.Height];
            _pathGraph = new BattlePathGraph(this);
            _hurtMap = new HurtMap(_terrain);

            // Make a bunch of queues for the attackers remaining to be spawned.
            _remainingAttackerSpawns = new SpawnQueueCluster(_terrain, _attackPlans);

            var attackerBreachCounts = new Dictionary<int,int>();
            foreach (var teamId in _remainingAttackerSpawns.AttackerTeamIds)
                attackerBreachCounts[teamId] = 0;

            // Spawn all defenders
            foreach (var plan in _defensePlans)
            {
                foreach (var placement in plan.Placements)
                {
                    var id = GenerateId(0.0, placement.UnitType);
                    var classChar = _unitTypeMap[placement.UnitType];
                    var newEntity = new BattleEntity(id, classChar, plan.TeamId, false);
                    newEntity.Spawn(this, placement.Position);
                    _entities.Add(newEntity);
                    _events.Add(CreateEvent(0.0, BattleEventType.Spawn, newEntity, null));
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

                // Update existing entities
                var entitiesReachingGoal = new List<BattleEntity>();
                foreach (var entity in _entities)
                {
                    var newEvent = entity.Update(this, time, _timeSlice);
                    if (newEvent != null)
                    {
                        _events.Add(newEvent);

                        // If the entity moved, check to see if it reached its goal and take note.
                        if (newEvent.Type==BattleEventType.EndMovement)
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
                    _events.Add(CreateEvent(time, BattleEventType.ReachesGoal, winEnt, null));
                    _events.Add(CreateEvent(time, BattleEventType.Despawn, winEnt, null));
                    winEnt.PrepareToDespawn(this);
                    _entities.Remove(winEnt);

                    attackerBreachCounts[winEnt.TeamId] += 1;
                }

                // Remove entities that just died
                var deadEntities = _entities.Where( (ent) => ent.HitPoints<=0 );
                _entities = _entities.Except(deadEntities).ToList();
                foreach (var deadEnt in deadEntities)
                {
                    _events.Add(CreateEvent(time, BattleEventType.Die, deadEnt, null));
                    _events.Add(CreateEvent(time, BattleEventType.Despawn, deadEnt, null));
                    deadEnt.PrepareToDespawn(this);
                    _hurtMap.InvalidateTeam(deadEnt.TeamId);
                }

                // Spawn new entities
                SpawnAttackers(time);

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

            _logger.Debug("Resolution pathfinding stats: " + _pathGraph.DebugInfo() );

            return new BattleResolution()
            {
                Terrain = _terrain,
                UnitTypes = _unitTypeMap.Values.ToList(),
                Events = _events,
                AttackerBreachCounts = attackerBreachCounts,
            };
        }

        internal HurtMap HurtMap => _hurtMap;

        internal BattleEntity GetEntityAt(Vector2Di position)
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

        internal void SetEntityAt(Vector2Di position, BattleEntity entity)
        {
            _blockedPositions[position.X, position.Y] = entity;
        }

        internal void ClearEntityAt(Vector2Di position)
        {
            _blockedPositions[position.X, position.Y] = null;
        }

        internal IList<Vector2Di> GetGoalTiles(int teamId)
        {
            return this._terrain.GoalPointsMap[teamId];
        }

        internal IList<Vector2Di> FindPathToGoal(BattleEntity entity)
        {
            var path = _pathGraph.FindPathToGoal(this, entity);
            return path;
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private System.Diagnostics.Stopwatch _runTimer;
        private Terrain _terrain;
        private List<AttackPlan> _attackPlans;
        private List<DefensePlan> _defensePlans;

        private List<BattleEvent> _events;
        private List<BattleEntity> _entities;

        private BattleEntity[,] _blockedPositions;

        private BattlePathGraph _pathGraph;

        private Dictionary<string, UnitCharacteristics> _unitTypeMap;

        private int _nextId;
        private HurtMap _hurtMap;
        private SpawnQueueCluster _remainingAttackerSpawns;

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

        private BattleEntity TrySpawnNextAttacker(double time, AttackPlan plan)
        {
            var nextSpawnCommand = (plan.Spawns.Count>0)? plan.Spawns[0] : null;
            if (nextSpawnCommand != null && nextSpawnCommand.Time<=time)
            {
                var spawnPos = _terrain.SpawnPointsMap[plan.TeamId][nextSpawnCommand.SpawnPointIndex];
                var isBlocked = GetEntityAt(spawnPos) != null;
                if (!isBlocked)
                {
                    var id = GenerateId(time, nextSpawnCommand.UnitType);
                    var classChar = _unitTypeMap[nextSpawnCommand.UnitType];
                    var newEntity = new BattleEntity(id, classChar, plan.TeamId, true);
                    newEntity.Spawn(this, spawnPos);

                    // TODO: use a better data structure.  This is stupid.
                    plan.Spawns.RemoveAt(0);

                    return newEntity;
                }
            }

            return null;
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
                    if (GetEntityAt(pos) != null)
                        continue;

                    // Get the next spawn command for this team and spawnPoint, if any, as long as its
                    // spawn time is <= the current time.
                    var spawnDef = _remainingAttackerSpawns.GetNext(teamId, spawnPtIdx, time);
                    if (spawnDef != null)
                    {
                        var id = GenerateId(time, spawnDef.UnitType);
                        var classChar = _unitTypeMap[spawnDef.UnitType];
                        var newEntity = new BattleEntity(id, classChar, teamId, true);
                        newEntity.Spawn(this, pos);

                        _entities.Add(newEntity);
                        _events.Add(CreateEvent(time, BattleEventType.Spawn, newEntity, null));
                    }
                }
            }
        }

        private string GenerateId(double time, string cls)
        {
            var id = $"{cls}{_nextId.ToString()}";
            _nextId += 1;
            return id;
        }
    }
}