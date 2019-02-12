using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Something that exists in the game world, such as an attacker, defender, or blockade.
    /// </summary>
    internal class BattleEntity
    {
        public string Id { get; }
        public UnitCharacteristics Class { get; }
        public bool IsAttacker { get; }
        public double SpeedTilesPerSec { get; private set; }
        public int HitPoints { get; private set; }
        public Vector2Di Position { get; private set; }
        public Vector2Di? MovingToPosition { get; private set; }
        public Action CurrentAction { get; private set; }
        public double CurrentActionElapsedTime { get; private set; }
        public string AttackTargetId { get; private set; }
        public int TeamId { get; }
        public double WeaponReloadElapsedTime { get; private set; }
        public string BerserkTargetId { get; private set; }

        public BattleEntity(string id, UnitCharacteristics clsChar, int teamId, bool isAttacker)
        {
            this.Id = id;
            this.Class = clsChar;
            this.IsAttacker = isAttacker;
            this.HitPoints = clsChar.InitialHitPoints;
            this.Position = new Vector2Di(short.MinValue, short.MinValue);
            this.CurrentAction = Action.None;
            this.CurrentActionElapsedTime = 0.0;
            this.TeamId = teamId;
            this.WeaponReloadElapsedTime = 0.0;

            // Only attackers are allowed to move.
            this.SpeedTilesPerSec = (isAttacker)? clsChar.SpeedTilesPerSec : 0.0;
        }

        public BattleEvent Update(BattleState battleState, double time, double deltaSeconds)
        {
            // Weapon cooldown advances regardless of whatever else the entity is doing this timeslice.
            this.WeaponReloadElapsedTime += deltaSeconds;

            switch (this.CurrentAction)
            {
                case Action.None:
                    return UpdateNone(battleState, time, deltaSeconds);
                case Action.Move:
                    return UpdateMove(battleState, time, deltaSeconds);
                case Action.Attack:
                    return UpdateAttack(battleState, time, deltaSeconds);
                default:
                    throw new NotImplementedException();
            }
        }

        public void Spawn(BattleState battleState, Vector2Di pos)
        {
            this.Position = pos;
            battleState.SetEntityAt(pos, this);
        }

        public void PrepareToDespawn(BattleState battleState)
        {
            battleState.ClearEntityAt(this.Position);
            if (this.MovingToPosition.HasValue)
                battleState.ClearEntityAt(this.MovingToPosition.Value);
            this.Position = new Vector2Di(short.MinValue, short.MinValue);
            this.MovingToPosition = null;
        }

        public void ApplyDamage(int damageAmount)
        {
            // Some day there might be mitigating factors - damage types, direction, debufs, etc.
            this.HitPoints -= damageAmount;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var objEntity = obj as BattleEntity;
            return this.Id.Equals(objEntity.Id);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public Queue<Vector2Di> _plannedPath;
        public Queue<Vector2Di> _plannedBerserkPath;

        private BattleEvent UpdateNone(BattleState battleState, double time, double deltaSeconds)
        {
            switch (this.Class.Behavior)
            {
                case UnitBehavior.None:
                    return ChooseActionNone(battleState, time, deltaSeconds);
                case UnitBehavior.Rusher:
                    return ChooseActionRusher(battleState, time, deltaSeconds);
                case UnitBehavior.Marcher:
                    return ChooseActionMarcher(battleState, time, deltaSeconds);
                case UnitBehavior.Berserker:
                    return ChooseActionBerserker(battleState, time, deltaSeconds);
                default:
                    throw new NotImplementedException("Unit behavior not implemented.");
            }
        }
        private BattleEvent UpdateMove(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.Move);
            Debug.Assert(this.MovingToPosition.HasValue);
            Debug.Assert(this.SpeedTilesPerSec > 0);
            Debug.Assert(battleState.GetEntityAt(this.MovingToPosition.Value).Id==this.Id);

            var timeToMove = 1.0/this.SpeedTilesPerSec;
            this.CurrentActionElapsedTime += deltaSeconds;
            if (this.CurrentActionElapsedTime >= timeToMove)
            {
                // Clear our lock on our old position.
                Debug.Assert(battleState.GetEntityAt(this.Position).Id==this.Id);
                battleState.ClearEntityAt(this.Position);

                var evt = new BattleEvent()
                {
                    Time = time,
                    Type = BattleEventType.EndMovement,
                    SourceEntity = this.Id,
                    SourceLocation = this.Position,
                    TargetLocation = this.MovingToPosition,
                };

                this.CurrentAction = Action.None;
                this.CurrentActionElapsedTime = 0.0;
                this.Position = this.MovingToPosition.Value;
                this.MovingToPosition = null;

                return evt;
            }

            return null;
        }
        private BattleEvent UpdateAttack(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.Attack);

            this.CurrentActionElapsedTime += deltaSeconds;
            if (this.CurrentActionElapsedTime >= this.Class.WeaponUseTime)
            {
                // Decrease the target's hitpoints (assuming it still exists).  Some day we might need to expand this
                // with damage types, facing, debufs, etc.
                var target = battleState.GetEntityById(this.AttackTargetId);
                if (target!=null)
                {
                    target.HitPoints -= this.Class.WeaponDamage;
                }

                // Create an event.  If the target doesn't exist, we still create the event.
                var evt = new BattleEvent()
                {
                    Time = time,
                    Type = BattleEventType.EndAttack,
                    SourceEntity = this.Id,
                    SourceLocation = this.Position,
                    SourceTeamId = this.TeamId,
                    TargetEntity = target?.Id,
                    TargetLocation = target?.Position,
                    TargetTeamId = target?.TeamId ?? 0,
                    DamageAmount = this.Class.WeaponDamage,
                };

                this.CurrentAction = Action.None;
                this.CurrentActionElapsedTime = deltaSeconds;
                this.WeaponReloadElapsedTime = 0.0;

                return evt;
            }

            return null;
        }

        private BattleEvent TryBeginMove(BattleState battleState, double time, double deltaSeconds, Vector2Di toPos)
        {
            // If speed is zero, obviously, no move.
            if (this.SpeedTilesPerSec <= 0)
                return null;

            // If the target tile is blocked, we can't move.
            if (battleState.GetEntityAt(toPos) != null)
                return null;

            this.CurrentAction = Action.Move;
            this.CurrentActionElapsedTime = deltaSeconds;
            this.MovingToPosition = toPos;

            // Reserve the tile we're moving into as well as the one we're in.
            battleState.SetEntityAt(toPos, this);

            var evt = new BattleEvent()
            {
                Time = time,
                Type = BattleEventType.BeginMovement,
                SourceEntity = this.Id,
                SourceLocation = this.Position,
                TargetLocation = this.MovingToPosition,
            };
            return evt;
        }

        private BattleEvent TryBeginAttack(BattleState battleState, double time, double deltaSeconds, BattleEntity target)
        {
            // Don't attack if we can't actually do damage.
            if (this.Class.WeaponDamage<=0 || this.Class.WeaponRangeTiles<=1.0)
                return null;

            // Don't attack if the weapon isn't ready.
            if (this.WeaponReloadElapsedTime<this.Class.WeaponReloadTime)
                return null;

            // If the target is too far away, we can't attack.
            if (this.Position.DistanceTo(target.Position)>this.Class.WeaponRangeTiles)
                return null;

            this.CurrentAction = Action.Attack;
            this.CurrentActionElapsedTime = 0.0;
            this.AttackTargetId = target.Id;

            var evt = new BattleEvent()
            {
                Time = time,
                Type = BattleEventType.BeginAttack,
                SourceEntity = this.Id,
                SourceLocation = this.Position,
                TargetEntity = target.Id,
                TargetLocation = target.Position,
                TargetTeamId = target.TeamId,
            };
            return evt;
        }

        /// <summary>
        /// Don't do anything.  Don't even dream about doing things.
        /// </summary>
        private BattleEvent ChooseActionNone(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.None);
            Debug.Assert(this.Class.Behavior==UnitBehavior.None);

            return null;
        }

        /// <summary>
        /// Chooses the unit's next action, using the Rusher behavior.  Rushers try to move whenever they can
        /// and only attack when their path is blocked.
        /// </summary>
        private BattleEvent ChooseActionRusher(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.None);
            Debug.Assert(this.Class.Behavior==UnitBehavior.Rusher);

            BattleEvent actionEvent = null;

            // Move, if possible.
            actionEvent = ChooseActionMoveIfPossible(battleState, time, deltaSeconds);

            // If we can't move, try to attack something - preferably whatever's blocking our path if
            // it's an enemy.
            if (actionEvent==null)
                actionEvent = ChooseActionAttackIfPossible(battleState, time, deltaSeconds);

            if (actionEvent==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  Replot path next time around.
                    _plannedPath = null;
                    _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                }
            }

            return actionEvent;
        }

        /// <summary>
        /// Chooses the unit's next action, using the Marcher behavior.  Marchers will attack when there's
        /// an enemy in range and their weapon is ready, but otherwise they will keep moving.
        /// </summary>
        private BattleEvent ChooseActionMarcher(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.None);
            Debug.Assert(this.Class.Behavior==UnitBehavior.Marcher);

            BattleEvent actionEvent = null;

            // Attack something, if anything is in range/LOS and our weapon is ready.
            actionEvent = ChooseActionAttackIfPossible(battleState, time, deltaSeconds);

            // Try to move if there's nothing to attack.
            if (actionEvent==null)
                actionEvent = ChooseActionMoveIfPossible(battleState, time, deltaSeconds);

            if (actionEvent==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  Replot path next time around.
                    _plannedPath = null;
                    _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                }
            }

            return actionEvent;
        }

        private BattleEvent ChooseActionBerserker(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.None);
            Debug.Assert(this.Class.Behavior==UnitBehavior.Berserker);

            BattleEvent actionEvent = null;

            // First priority of a berserker, ATTAAAAACCCKKKK!!!
            actionEvent = ChooseActionAttackIfPossible(battleState, time, deltaSeconds);

            // Second priority of a berserker, run toward someone you can ATTAAAAACCCKKKK!!!
            if (actionEvent==null)
                actionEvent = ChooseActionMoveTowardEnemy(battleState, time, deltaSeconds);

            // Third priority of a berserker, I don't know, march down the road or something.
            if (actionEvent==null && _plannedBerserkPath==null)
                actionEvent = ChooseActionMoveIfPossible(battleState, time, deltaSeconds);

            if (actionEvent==null && _plannedBerserkPath==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  Replot path next time around.
                    _plannedPath = null;
                    _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                }
            }

            return actionEvent;
        }

        private BattleEvent ChooseActionAttackIfPossible(BattleState battleState, double time, double deltaSeconds)
        {
            BattleEvent actionEvent = null;

            bool weaponReady = this.Class.WeaponDamage>0
                && this.Class.WeaponRangeTiles>0
                && this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime;
            if (weaponReady)
            {
                // If we're able to attack, prioritize whatever is directly in our path.  The point here is
                // to reduce the time attackers might block a bottleneck.
                if (_plannedPath!=null && _plannedPath.Count>0)
                {
                    var entityInNextPos = battleState.GetEntityAt(_plannedPath.Peek());
                    if (entityInNextPos!=null && entityInNextPos.TeamId!=this.TeamId)
                    {
                        actionEvent = TryBeginAttack(battleState, time, deltaSeconds, entityInNextPos);

                        if (_logger.IsDebugEnabled && actionEvent!=null)
                            _logger.Trace("{0} is attacking {1} because it's in their path", this.Id, entityInNextPos.Id);

                    }
                }

                // The next priority is the closest in-range enemy, if there is one.
                // (We might need to allow different target priorites at some point.)
                if (actionEvent==null)
                {
                    var closestEnemy = ListEnemiesInRange(battleState)
                        .OrderBy( (ent) => this.Position.DistanceTo(ent.Position) )
                        .FirstOrDefault();
                    if (closestEnemy != null)
                        actionEvent = TryBeginAttack(battleState, time, deltaSeconds, closestEnemy);

                    if (_logger.IsDebugEnabled && actionEvent!=null)
                        _logger.Trace("{0} is attacking {1} because it's in range", this.Id, closestEnemy.Id);
                }
            }

            return actionEvent;
        }

        private BattleEvent ChooseActionMoveIfPossible(BattleState battleState, double time, double deltaSeconds)
        {
            BattleEvent actionEvent = null;

            // If this is a mobile unit, try to move, or attack whatever's in the way.
            // Defenders can't move, even if their class can when they're on attack.
            if (this.SpeedTilesPerSec>0 && this.IsAttacker)
            {
                if (_plannedPath==null || _plannedPath.Count==0)
                    ChoosePathToGoal(battleState);

                var nextPos = _plannedPath.Peek();

                // This should be an adjacent tile.
                Debug.Assert(this.Position.DistanceTo(nextPos)<=1.5);

                var entityInNextPos = battleState.GetEntityAt(nextPos);

                if (entityInNextPos==null)
                {
                    // Nothing to stop us moving into the next tile in out planned path.
                    _plannedPath.Dequeue();
                    actionEvent = TryBeginMove(battleState, time, deltaSeconds, nextPos);

                    if (_logger.IsDebugEnabled && actionEvent!=null)
                        _logger.Trace("{0} is moving toward {1} because it's in their path", this.Id, nextPos);
                }
            }

            return actionEvent;
        }

        private BattleEvent ChooseActionMoveTowardEnemy(BattleState battleState, double time, double deltaSeconds)
        {
            const double berserkerAggroRange = 14;

            BattleEvent actionEvent = null;

            if (this.SpeedTilesPerSec>0 && this.IsAttacker)
            {
                // If our last target has despawned, clear the path.
                if (_plannedBerserkPath!=null)
                {
                    if (battleState.GetEntityById(this.BerserkTargetId)==null)
                    {
                        _plannedBerserkPath = null;
                        this.BerserkTargetId = null;
                        _logger.Trace("{0} is rethinking its berserk path because its target is dead", this.Id);
                    }
                }

                // If there are enemies in sight that we have straight paths to, charge one.
                if (_plannedBerserkPath==null)
                {
                    var potentialTargets = ListBerserkerTargetsInRange(battleState, berserkerAggroRange).ToList();
                    if (potentialTargets.Count>0)
                    {
                        // This list contains only enemies that, considering only terrain, we have a straight path to.  But
                        // we don't want all of our berserkers to follow a conga line, so we'll use our regular pathfinding
                        // to choose a path that tries to go around friendly units, as needed.  Berserkers are much more interesting
                        // when the behave like a hoard rather than a line at the DMV.
                        var potentialTargetLocs = potentialTargets.Select( (targ) => targ.Position );
                        var pathToTarget = battleState.FindPathToSomewhere(this, potentialTargetLocs);
                        if (pathToTarget!=null)
                        {
                            var pathEnd = pathToTarget[pathToTarget.Count-1];
                            var target = potentialTargets.Where( (targ) => targ.Position==pathEnd ).First();
                            this.BerserkTargetId = target.Id;
                            _plannedBerserkPath = new Queue<Vector2Di>(pathToTarget);
                            _plannedPath = null;
                            _logger.Trace("{0} is planning a berserker charge on {1}, {2} steps away", this.Id, target.Id, pathToTarget.Count);
                        }
                    }
                }

                if (_plannedBerserkPath!=null)
                {
                    var nextPos = _plannedBerserkPath.Peek();
                    var entityInNextPos = battleState.GetEntityAt(nextPos);

                    if (entityInNextPos==null)
                    {
                        // Nothing to stop us moving into the next tile in out planned path.
                        _plannedBerserkPath.Dequeue();
                        actionEvent = TryBeginMove(battleState, time, deltaSeconds, nextPos);

                        if (_logger.IsDebugEnabled && actionEvent!=null)
                            _logger.Trace("{0} is berserker charging into {1}", this.Id, nextPos);
                    }
                    else
                    {
                        // If the thing in our way is an enemy, do nothing while we ready our weapon to hit
                        // it again.  If it's a friend, wait for it to go away.
                        _logger.Trace("{0} is not moving - something is in its berserker path.", this.Id);
                    }
                }
            }

            return actionEvent;
        }

        private void ChoosePathToGoal(BattleState battleState)
        {
            var path = battleState.FindPathToGoal(this);
            _plannedPath = new Queue<Vector2Di>(path);
        }

        private IEnumerable<BattleEntity> ListEnemiesInRange(BattleState battleState)
        {
            Func<BattleEntity,bool> isEnemy = (ent) => ent.TeamId != this.TeamId;
            Func<BattleEntity,bool> inRange = (ent) => this.Position.DistanceTo(ent.Position)<=this.Class.WeaponRangeTiles;
            Func<BattleEntity,bool> visible = (ent) => battleState.Terrain.HasLineOfSight(this.Position, ent.Position);

            return battleState.GetAllEntities()
                .Where(isEnemy)
                .Where(inRange)
                .Where(visible);
        }

        private IEnumerable<BattleEntity> ListBerserkerTargetsInRange(BattleState battleState, double range)
        {
            Func<BattleEntity,bool> isEnemy = (ent) => ent.TeamId != this.TeamId;
            Func<BattleEntity,bool> canFight = (ent) => ent.Class.WeaponDamage>0 && ent.Class.WeaponRangeTiles>0;
            Func<BattleEntity,bool> inRange = (ent) => this.Position.DistanceTo(ent.Position)<=range;
            Func<BattleEntity,bool> reachable = (ent) => battleState.Terrain.StraightWalkablePath(this.Position, ent.Position)!=null;

            return battleState.GetAllEntities()
                .Where(isEnemy)
                .Where(canFight)
                .Where(inRange)
                .Where(reachable);
        }
    }
}
