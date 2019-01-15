using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    public sealed class BattleState
    {
        public BattleResolution Resolve(Scenario scenario)
        {
            _terrain = scenario.Terrain;
            _attackPlans = new List<AttackPlan>(scenario.AttackPlans);
            _defensePlans = new List<DefensePlan>(scenario.DefensePlans ?? Enumerable.Empty<DefensePlan>());

            // Make a lookup table for unit types.
            _unitTypeMap = new Dictionary<string, UnitCharacteristics>();
            foreach (var unitType in scenario.UnitTypes)
                _unitTypeMap.Add(unitType.Name, unitType);

            // TODO: validate
            // * terrain is passable from spawn to goal
            // * attack and defense plans use only legal units
            // * defenders placed in legal locations
            // * attack spawns are in legal places and times

            _events = new List<BattleEvent>();
            _entities = new List<BattleEntity>();
            _entityPositions = new Dictionary<Vector2Di, BattleEntity>();
            _pathGraph = new BattlePathGraph() { Terrain = scenario.Terrain };

            var attackerBreachCounts = new Dictionary<int,int>();

            // Make copies of the attack plans to keep track of what's left to spawn.  Reverse-order
            // by time so that removing the next item from the list isn't costly.
            var remainingAttackerSpawns = new List<AttackPlan>();
            foreach (var plan in _attackPlans)
            {
                var copy = new AttackPlan()
                {
                    TeamId = plan.TeamId,
                    Spawns = plan.Spawns.OrderByDescending( (spawn) => spawn.Time ).ToList(),
                };
                remainingAttackerSpawns.Add(copy);

                attackerBreachCounts[plan.TeamId] = 0;
            }

            // Spawn all defenders
            foreach (var plan in _defensePlans)
            {
                foreach (var placement in plan.Placements)
                {
                    var id = GenerateId(0.0, placement.UnitType);
                    var classChar = _unitTypeMap[placement.UnitType];
                    var newEntity = new BattleEntity(id, classChar, plan.TeamId);
                    newEntity.Spawn(this, placement.Position);
                    _entities.Add(newEntity);
                    _events.Add(CreateEvent(0.0, BattleEventType.Spawn, newEntity, null));
                }
            }

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
                }

                // Spawn new entities
                foreach (var plan in remainingAttackerSpawns)
                {
                    bool keepSpawning = true;
                    while (keepSpawning)
                    {
                        var newEntity = TrySpawnNextAttacker(time, plan);
                        if (newEntity != null)
                        {
                            _entities.Add(newEntity);
                            _events.Add(CreateEvent(time, BattleEventType.Spawn, newEntity, null));
                        }
                        else
                        {
                            keepSpawning = false;
                        }
                    }
                }

                // End things if there are no mobile units left (and nothing left to spawn),
                // or if the time gets too high.
                var noMobileUnitsLeft = false;
                var spawnListsAreEmpty = remainingAttackerSpawns.All( (plan) => plan.Spawns.Count==0 );
                if (spawnListsAreEmpty)
                {
                    noMobileUnitsLeft = !_entities.Any( (ent) => ent.Class.SpeedTilesPerSec>0 );
                }

                battleEnded = (time >= _maxTime)
                    || noMobileUnitsLeft;
            }

            return new BattleResolution()
            {
                Terrain = _terrain,
                UnitTypes = scenario.UnitTypes,
                Events = _events,
                AttackerBreachCounts = attackerBreachCounts,
            };
        }

        internal BattleEntity GetEntityAt(Vector2Di position)
        {
            BattleEntity entity = null;
            _entityPositions.TryGetValue(position, out entity);
            return entity;
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
            _entityPositions.Add(position, entity);
        }

        internal void ClearEntityAt(Vector2Di position)
        {
            _entityPositions.Remove(position);
        }

        internal bool HasLineOfSight(Vector2Di fromPos, Vector2Di toPos)
        {
            // Quick and dirty ray-casting algorithm.  This might be adequate for our needs, but
            // see this blog post for a discussion of different algorithms and their properties:
            //   http://www.adammil.net/blog/v125_Roguelike_Vision_Algorithms.html#raycode

            // Quick check if the start and end locations are the same, to avoid division by zero.
            if (fromPos==toPos)
                return _terrain.GetTile(fromPos.X, fromPos.Y).BlocksVision;

            var dX = toPos.X - fromPos.X;
            var dY = toPos.Y - fromPos.Y;

            // Figure out if we're going mostly up-down or mostly left-right.  We want the axis that changes
            // more rapidly to be our independent axis.
            if (Math.Abs(dX) > Math.Abs(dY))
            {
                // For each integer X value, only look at the closest integer Y value alone the line.
                short incX = (short)Math.Sign(dX);
                double slope = (double)dY / (double)dX;
                for (short lineX=fromPos.X; lineX!=toPos.X; lineX += incX)
                {
                    double lineYfloat = (lineX-fromPos.X)*slope + fromPos.Y;
                    short lineY = (short)Math.Round(lineYfloat);
                    if (_terrain.GetTile(lineX, lineY).BlocksVision)
                        return false;
                }
            }
            else
            {
                short incY = (short)Math.Sign(dY);
                double slope = (double)dX / (double)dY;
                for (short lineY=fromPos.Y; lineY!=toPos.Y; lineY += incY)
                {
                    double lineXfloat = (lineY-fromPos.Y)*slope + fromPos.X;
                    short lineX = (short)Math.Round(lineXfloat);
                    if (_terrain.GetTile(lineX, lineY).BlocksVision)
                        return false;
                }
            }

            return true;
        }

        internal IList<Vector2Di> GetGoalTiles(int teamId)
        {
            return this._terrain.GoalPointsMap[teamId];
        }

        internal IList<Vector2Di> FindPathToGoal(int teamId, Vector2Di startPos)
        {
            var path = _pathGraph.FindPathToGoal(teamId, startPos);
            return path;
        }

        private Terrain _terrain;
        private List<AttackPlan> _attackPlans;
        private List<DefensePlan> _defensePlans;

        private List<BattleEvent> _events;
        private List<BattleEntity> _entities;

        private Dictionary<Vector2Di, BattleEntity> _entityPositions;

        private BattlePathGraph _pathGraph;

        private Dictionary<string, UnitCharacteristics> _unitTypeMap;

        private int _nextId;

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
            // Try to create the next attacker in the list (which is ordered high to low by time).
            // If we're past the spawn's time, and the spawn location isn't blocked, we can do it.
            // If not, we'll try again later.
            var nextSpawnCommand = (plan.Spawns.Count>0)? plan.Spawns[plan.Spawns.Count-1] : null;
            if (nextSpawnCommand != null && nextSpawnCommand.Time<=time)
            {
                var spawnPos = _terrain.SpawnPointsMap[plan.TeamId][nextSpawnCommand.SpawnPointIndex];
                var isBlocked = GetEntityAt(spawnPos) != null;
                if (!isBlocked)
                {
                    var id = GenerateId(time, nextSpawnCommand.UnitType);
                    var classChar = _unitTypeMap[nextSpawnCommand.UnitType];
                    var newEntity = new BattleEntity(id, classChar, plan.TeamId);
                    newEntity.Spawn(this, spawnPos);

                    plan.Spawns.RemoveAt(plan.Spawns.Count-1);

                    return newEntity;
                }
            }

            return null;
        }

        private string GenerateId(double time, string cls)
        {
            var id = $"{cls}{_nextId.ToString()}";
            _nextId += 1;
            return id;
        }
    }
}