using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;

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
        public Vector2Di? AttackTargetInitialPosition { get; private set; }
        public int TeamId { get; }
        public double WeaponReloadElapsedTime { get; private set; }

        public double TimeToLive { get; set; } = double.PositiveInfinity;

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

            _elapsedSecondsDoingNothing = 0.0;

            // Only attackers are allowed to move.
            this.SpeedTilesPerSec = (isAttacker)? clsChar.SpeedTilesPerSec : 0.0;
        }

        public BattleEvent Update(BattleState battleState, double time, double deltaSeconds)
        {
            // Weapon cooldown advances regardless of whatever else the entity is doing this timeslice.
            this.WeaponReloadElapsedTime += deltaSeconds;

            // Decrease the lifetime timer.
            this.TimeToLive -= deltaSeconds;

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
            if (this.Class.BlocksTile)
                battleState.SetEntityBlockingTile(pos, this);
        }

        public void PrepareToDespawn(BattleState battleState)
        {
            if (this.Class.BlocksTile)
            {
                battleState.ClearEntityBlockingTile(this.Position);
                if (this.MovingToPosition.HasValue)
                    battleState.ClearEntityBlockingTile(this.MovingToPosition.Value);
            }
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

        public void ForceRepath()
        {
            _plannedPath = null;
            _berserkTargetId = null;
            _logger.Trace("{0} is rethinking their path because BattleState said to", this.Id);
        }

        /// <summary>
        /// Magic number that influences how long a unit is willing to wait when blocked by friendlies before
        /// re-calculating its path.
        /// </summary>
        private const double _repathAfterIdleFactor = 0.99;
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private Queue<Vector2Di> _plannedPath;
        private double _elapsedSecondsDoingNothing;
        private string _berserkTargetId;

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
            Debug.Assert(battleState.GetEntityBlockingTile(this.MovingToPosition.Value).Id==this.Id);

            var timeToMove = 1.0/this.SpeedTilesPerSec;
            this.CurrentActionElapsedTime += deltaSeconds;
            if (this.CurrentActionElapsedTime >= timeToMove)
            {
                // Clear our lock on our old position.
                Debug.Assert(battleState.GetEntityBlockingTile(this.Position).Id==this.Id);

                if (this.Class.BlocksTile)
                    battleState.ClearEntityBlockingTile(this.Position);

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

            BattleEvent evt = null;
            this.CurrentActionElapsedTime += deltaSeconds;
            if (this.CurrentActionElapsedTime >= this.Class.WeaponUseTime)
            {
                switch (this.Class.WeaponType)
                {
                    case WeaponType.Physical:
                        evt = FinishAttackPhysical(battleState, time, deltaSeconds);
                        break;
                    case WeaponType.Flamestrike:
                        evt = FinishAttackFlamestrike(battleState, time, deltaSeconds);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                this.CurrentAction = Action.None;
                this.CurrentActionElapsedTime = deltaSeconds;
                this.WeaponReloadElapsedTime = 0.0;
                this.AttackTargetId = null;
                this.AttackTargetInitialPosition = null;
            }

            return evt;
        }

        private BattleEvent FinishAttackPhysical(BattleState battleState, double time, double deltaSeconds)
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

            return evt;
        }

        private BattleEvent FinishAttackFlamestrike(BattleState battleState, double time, double deltaSeconds)
        {
            const string fireClassName = "Fire";
            const double lineFadeOutTimeSeconds = 1.0;

            Debug.Assert(this.AttackTargetInitialPosition.HasValue);

            // Look for the line that will hit the most enemies, as of right now.  If there aren't actually
            // any enemies in range any more, use the location of the one that caught our eye when we started
            // the attack.  They're dead or out of range, but we need to shoot somewhere.
            var targetPos = this.AttackTargetInitialPosition.Value;
            var bestTarget = ChooseFlamestrikeTarget(battleState);
            if (bestTarget != null)
                targetPos = bestTarget.Position;

            var tilesInLine = battleState.Terrain.StraightLinePath(this.Position, targetPos, this.Class.WeaponRangeTiles);
            double timeToLive = this.Class.WeaponDamage / 100.0;
            foreach (var pos in tilesInLine)
            {
                // Stop the line of fire if we hit the edge of the world or something wall-like.
                if (!battleState.Terrain.IsInBounds(pos) || battleState.Terrain.GetTile(pos).BlocksMovement)
                    break;

                // Magically miss tiles with friendly units in them.  (This isn't perfect - there's a delay
                // between processing this entity and spawning the new fire in which a mobile friendly unit
                // could move into the square.)
                var entityInTile = battleState.GetEntityBlockingTile(pos);
                if (entityInTile!=null && entityInTile.TeamId==this.TeamId)
                    continue;

                battleState.RequestSpawn(fireClassName, 0, false, pos, timeToLive);
                timeToLive += lineFadeOutTimeSeconds / this.Class.WeaponRangeTiles;
            }

            var evt = new BattleEvent()
            {
                Time = time,
                Type = BattleEventType.EndAttack,
                SourceEntity = this.Id,
                SourceLocation = this.Position,
                SourceTeamId = this.TeamId,
                TargetEntity = bestTarget?.Id,
                TargetLocation = targetPos,
                TargetTeamId = bestTarget?.TeamId ?? 0,
            };

            // Perhaps this needs refinement, but c'mon!  A giant swath of fire just cut through your ranks!  If that
            // doesn't make you think about where you want to be, what does?
            battleState.ForceRepathAll();

            return evt;
        }

        private BattleEvent TryBeginMove(BattleState battleState, double time, double deltaSeconds, Vector2Di toPos)
        {
            // If speed is zero, obviously, no move.
            if (this.SpeedTilesPerSec <= 0)
                return null;

            // If the target tile is blocked, we can't move.
            if (battleState.GetEntityBlockingTile(toPos) != null)
                return null;

            this.CurrentAction = Action.Move;
            this.CurrentActionElapsedTime = deltaSeconds;
            this.MovingToPosition = toPos;

            // Reserve the tile we're moving into as well as the one we're in.
            if (this.Class.BlocksTile)
                battleState.SetEntityBlockingTile(toPos, this);

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

        private BattleEvent TryBeginPhysicalAttack(BattleState battleState, double time, double deltaSeconds, BattleEntity target)
        {
            // Don't attack if the entity is immune to attacks.
            if (!target.Class.Attackable)
                return null;

            // We're only concerned with physical attacks here.
            if (this.Class.WeaponType!=WeaponType.Physical)
                return null;

            // Don't attack if the weapon isn't ready.
            if (this.WeaponReloadElapsedTime<this.Class.WeaponReloadTime)
                return null;

            // If the target is too far away, we can't attack.
            if (this.Position.DistanceTo(target.Position)>this.Class.WeaponRangeTiles)
                return null;

            this.CurrentAction = Action.Attack;
            this.CurrentActionElapsedTime = deltaSeconds;
            this.AttackTargetId = target.Id;
            this.AttackTargetInitialPosition = target.Position;

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
            UpdatePathTowardGoal(battleState);
            actionEvent = MoveAlongPath(battleState, time, deltaSeconds);

            // If we can't move, try to attack something - preferably whatever's blocking our path if
            // it's an enemy.
            if (actionEvent==null)
                actionEvent = ChooseActionAttackIfPossible(battleState, time, deltaSeconds);

            if (actionEvent==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  If this happens too many times in a row, recalculate
                    // our path on the next cycle.  How long we wait is largely determined by CrowdAversionBias.
                    _elapsedSecondsDoingNothing += deltaSeconds;
                    if (this.Class.CrowdAversionBias>0 && _elapsedSecondsDoingNothing >= _repathAfterIdleFactor/this.Class.CrowdAversionBias)
                    {
                        _plannedPath = null;
                        _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                    }
                }
            }
            else
            {
                _elapsedSecondsDoingNothing = 0.0;
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
            {
                UpdatePathTowardGoal(battleState);
                actionEvent = MoveAlongPath(battleState, time, deltaSeconds);
            }

            if (actionEvent==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  If this happens too many times in a row, recalculate
                    // our path on the next cycle.  How long we wait is largely determined by CrowdAversionBias.
                    _elapsedSecondsDoingNothing += deltaSeconds;
                    if (this.Class.CrowdAversionBias>0 && _elapsedSecondsDoingNothing >= _repathAfterIdleFactor/this.Class.CrowdAversionBias)
                    {
                        _plannedPath = null;
                        _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                    }
                }
            }
            else
            {
                _elapsedSecondsDoingNothing = 0.0;
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

            if (actionEvent==null)
            {
                // Second priority of a berserker, run toward someone you can ATTAAAAACCCKKKK!!!
                UpdatePathTowardEnemy(battleState);

                // Third priority of a berserker, I don't know, march down the road or something.
                UpdatePathTowardGoal(battleState);

                actionEvent = MoveAlongPath(battleState, time, deltaSeconds);
            }

            if (actionEvent==null)
            {
                if (this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime && this.SpeedTilesPerSec>0)
                {
                    // If we could neither move nor attack, and yet our weapon is ready, there must be
                    // a friendly unit in the way.  If this happens too many times in a row, recalculate
                    // our path on the next cycle.  How long we wait is largely determined by CrowdAversionBias.
                    _elapsedSecondsDoingNothing += deltaSeconds;
                    if (this.Class.CrowdAversionBias>0 && _elapsedSecondsDoingNothing >= _repathAfterIdleFactor/this.Class.CrowdAversionBias)
                    {
                        _plannedPath = null;
                        _berserkTargetId = null;
                        _logger.Trace("{0} is rethinking their path because a friendly unit is in the way", this.Id);
                    }
                }
            }
            else
            {
                _elapsedSecondsDoingNothing = 0.0;
            }

            return actionEvent;
        }

        private BattleEvent ChooseActionAttackIfPossible(BattleState battleState, double time, double deltaSeconds)
        {
            switch (this.Class.WeaponType)
            {
                case WeaponType.Physical:
                    return AttackIfPossiblePhysical(battleState, time, deltaSeconds);
                case WeaponType.Flamestrike:
                    return AttackIfPossibleFlamestrike(battleState, time, deltaSeconds);
                default:
                    throw new NotImplementedException();
            }
        }

        private BattleEvent AttackIfPossiblePhysical(BattleState battleState, double time, double deltaSeconds)
        {
            BattleEvent actionEvent = null;

            bool weaponReady = this.Class.WeaponDamage>0
                && this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime;
            if (weaponReady)
            {
                // If we're able to attack, prioritize whatever is directly in our path.  The point here is
                // to reduce the time attackers might block a bottleneck.
                if (_plannedPath!=null && _plannedPath.Count>0)
                {
                    var entityInNextPos = battleState.GetEntityBlockingTile(_plannedPath.Peek());
                    if (entityInNextPos!=null && entityInNextPos.TeamId!=this.TeamId && entityInNextPos.Class.Attackable)
                    {
                        actionEvent = TryBeginPhysicalAttack(battleState, time, deltaSeconds, entityInNextPos);

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
                    {
                        actionEvent = TryBeginPhysicalAttack(battleState, time, deltaSeconds, closestEnemy);
                    }

                    if (_logger.IsDebugEnabled && actionEvent!=null)
                        _logger.Trace("{0} is attacking {1} because it's in range", this.Id, closestEnemy.Id);
                }
            }

            return actionEvent;
        }

        private BattleEvent AttackIfPossibleFlamestrike(BattleState battleState, double time, double deltaSeconds)
        {
            BattleEvent actionEvent = null;

            bool weaponReady = this.Class.WeaponDamage>0 && this.WeaponReloadElapsedTime>=this.Class.WeaponReloadTime;
            if (!weaponReady)
                return null;

            // Right now we don't care which enemy to attack, as long as there's at least one.  We'll
            // pick our line when we finish the attack.
            var firstEnemy = ListEnemiesInRange(battleState).FirstOrDefault();
            if (firstEnemy != null)
            {
                if (this.WeaponReloadElapsedTime<this.Class.WeaponReloadTime)
                    return null;

                this.CurrentAction = Action.Attack;
                this.CurrentActionElapsedTime = deltaSeconds;
                this.AttackTargetId = null;
                this.AttackTargetInitialPosition = firstEnemy.Position;

                actionEvent = new BattleEvent()
                {
                    Time = time,
                    Type = BattleEventType.BeginAttack,
                    SourceEntity = this.Id,
                    SourceLocation = this.Position,
                    TargetEntity = null,
                    TargetLocation = firstEnemy.Position,
                    TargetTeamId = firstEnemy.TeamId,
                };
            }

            if (_logger.IsDebugEnabled && actionEvent!=null)
                _logger.Trace("{0} is beginning a flamestrike", this.Id);

            return actionEvent;
        }

        private void UpdatePathTowardGoal(BattleState battleState)
        {
            // If this is a mobile unit, try to move, or attack whatever's in the way.
            // Defenders can't move, even if their class can when they're on attack.
            if (this.SpeedTilesPerSec>0 && this.IsAttacker)
            {
                if (_plannedPath==null || _plannedPath.Count==0)
                    FindPathToGoal(battleState);
            }
        }

        private void UpdatePathTowardEnemy(BattleState battleState)
        {
            const double berserkerAggroRange = 14;

            if (this.SpeedTilesPerSec>0 && this.IsAttacker)
            {
                // If our last target has despawned, clear the path.
                if (_berserkTargetId != null)
                {
                    if (battleState.GetEntityById(_berserkTargetId)==null)
                    {
                        _plannedPath = null;
                        _berserkTargetId = null;
                        _logger.Trace("{0} is rethinking its berserk path because its target is dead", this.Id);
                    }
                }

                // If we're not charging toward a living enemy, look for another one to charge.  (This happens
                // even if we're on a normal path-to-goal.)
                if (_berserkTargetId==null || _plannedPath==null || _plannedPath.Count==0)
                {
                    _berserkTargetId = null;
                    _plannedPath = null;

                    var potentialTargets = ListBerserkerTargetsInRange(battleState, berserkerAggroRange).ToList();
                    if (potentialTargets.Count>0)
                    {
                        // This list contains only enemies that, considering only terrain, we have a straight path to.  But
                        // we don't want all of our berserkers to follow a conga line, so we'll use our regular pathfinding
                        // to choose a path that tries to go around friendly units, as needed.  Berserkers are much more interesting
                        // when the behave like a hoard rather than a line at the DMV.
                        var potentialTargetLocs = potentialTargets.Select( (targ) => targ.Position );
                        var pathToTarget = battleState.PathGraph.FindPathToSomewhere(this, potentialTargetLocs);
                        if (pathToTarget!=null)
                        {
                            var pathEnd = pathToTarget[pathToTarget.Count-1];
                            var target = potentialTargets.Where( (targ) => targ.Position==pathEnd ).First();
                            _berserkTargetId = target.Id;
                            _plannedPath = new Queue<Vector2Di>(pathToTarget);
                            _logger.Trace("{0} is planning a berserker charge on {1}, {2} steps away", this.Id, target.Id, pathToTarget.Count);
                        }
                    }
                }
            }
        }

        private BattleEvent MoveAlongPath(BattleState battleState, double time, double deltaSeconds)
        {
            BattleEvent actionEvent = null;

            if (_plannedPath!=null && _plannedPath.Count>0)
            {
                var nextPos = _plannedPath.Peek();

                // This should be an adjacent tile.
                Debug.Assert(this.Position.DistanceTo(nextPos)<=1.5);

                var entityInNextPos = battleState.GetEntityBlockingTile(nextPos);

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

        private void FindPathToGoal(BattleState battleState)
        {
            var path = battleState.PathGraph.FindPathToGoal(this);
            _plannedPath = new Queue<Vector2Di>(path);
        }

        private IEnumerable<BattleEntity> ListEnemiesInRange(BattleState battleState)
        {
            Func<BattleEntity,bool> isAttackable = (ent) => ent.Class.Attackable;
            Func<BattleEntity,bool> isEnemy = (ent) => ent.TeamId != this.TeamId;
            Func<BattleEntity,bool> inRange = (ent) => this.Position.DistanceTo(ent.Position)<=this.Class.WeaponRangeTiles;
            Func<BattleEntity,bool> visible = (ent) => battleState.Terrain.HasLineOfSight(this.Position, ent.Position);

            return battleState.GetAllEntities()
                .Where(isAttackable)
                .Where(isEnemy)
                .Where(inRange)
                .Where(visible);
        }

        private IEnumerable<BattleEntity> ListBerserkerTargetsInRange(BattleState battleState, double range)
        {
            Func<BattleEntity,bool> isAttackable = (ent) => ent.Class.Attackable;
            Func<BattleEntity,bool> isEnemy = (ent) => ent.TeamId != this.TeamId;
            Func<BattleEntity,bool> canFight = (ent) => ent.Class.WeaponType != WeaponType.None;
            Func<BattleEntity,bool> inRange = (ent) => this.Position.DistanceTo(ent.Position)<=range;
            Func<BattleEntity,bool> reachable = (ent) => battleState.Terrain.StraightWalkablePath(this.Position, ent.Position)!=null;

            return battleState.GetAllEntities()
                .Where(isAttackable)
                .Where(isEnemy)
                .Where(canFight)
                .Where(inRange)
                .Where(reachable);
        }

        /// <summary>
        /// Choses a target for a flamestrike attack, based on how many enemies will be hit.
        /// </summary>
        private BattleEntity ChooseFlamestrikeTarget(BattleState battleState)
        {
            var enemiesList = ListEnemiesInRange(battleState).ToList();
            var enemyLocations = enemiesList.Select( (entity) => entity.Position );

            int bestCount = 0;
            BattleEntity bestEnemy = null;

            foreach (var enemy in enemiesList)
            {
                // Project a line to this enemy and count how many enemies are on it.
                var lineToEnemy = battleState.Terrain.StraightLinePath(this.Position, enemy.Position, this.Class.WeaponRangeTiles);
                var enemiesOnLine = enemyLocations.Intersect(lineToEnemy).Count();
                if (enemiesOnLine > bestCount
                    || (enemiesOnLine == bestCount && this.Position.DistanceTo(enemy.Position) < this.Position.DistanceTo(bestEnemy.Position)))
                {
                    bestCount = enemiesOnLine;
                    bestEnemy = enemy;
                }
            }

            _logger.Trace("{0} is flamestriking {1} enemies", this.Id, bestCount);

            return bestEnemy;
        }
    }
}
