using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Resolver
{
    internal class BattleEntity
    {
        public string Id { get; }
        public UnitCharacteristics Class { get; }
        public bool IsAttacker { get; }
        public int HitPoints { get; private set; }
        public Vector2Di Position { get; private set; }
        public Vector2Di? MovingToPosition { get; private set; }
        public Action CurrentAction { get; private set; }
        public double CurrentActionElapsedTime { get; private set; }
        public string AttackTargetId { get; private set; }
        public int TeamId { get; }
        public double WeaponReloadElapsedTime { get; private set; }
        public Queue<Vector2Di> PlannedPath { get; private set; }

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

        private BattleEvent UpdateNone(BattleState battleState, double time, double deltaSeconds)
        {
            return ChooseActionDefault(battleState, time, deltaSeconds);
        }
        private BattleEvent UpdateMove(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.Move);
            Debug.Assert(this.MovingToPosition.HasValue);
            Debug.Assert(this.Class.SpeedTilesPerSec >= 0);
            Debug.Assert(battleState.GetEntityAt(this.MovingToPosition.Value).Id==this.Id);

            var timeToMove = 1.0/this.Class.SpeedTilesPerSec;
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
            if (this.Class.SpeedTilesPerSec <= 0)
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
        /// Chooses the unit's next action, using default AI.
        /// </summary>
        private BattleEvent ChooseActionDefault(BattleState battleState, double time, double deltaSeconds)
        {
            Debug.Assert(this.CurrentAction==Action.None);

            // If this is a mobile unit, try to move, or attack whatever's in the way.
            // Defenders can't move, even if their class can when they're on attack.
            if (this.Class.SpeedTilesPerSec>0 && this.IsAttacker)
            {
                if (this.PlannedPath==null || this.PlannedPath.Count==0)
                    ChoosePath(battleState);

                var nextPos = this.PlannedPath.Peek();

                // This should be an adjacent tile.
                Debug.Assert(this.Position.DistanceTo(nextPos)<=1.5);

                var entityInNextPos = battleState.GetEntityAt(nextPos);

                if (entityInNextPos==null)
                {
                    // Nothing to stop us moving into the next tile in out planned path.
                    this.PlannedPath.Dequeue();
                    return TryBeginMove(battleState, time, deltaSeconds, nextPos);
                }
                else if (entityInNextPos.TeamId!=this.TeamId)
                {
                    // The thing in our way is an enemy.  KILL IT!
                    return TryBeginAttack(battleState, time, deltaSeconds, entityInNextPos);
                }

                // If the thing in our way is friendly, fall through to the next section where we look
                // for something to kill while we wait.
            }

            if (this.Class.WeaponRangeTiles>0)
            {
                var closestEnemy = ListEnemiesInRange(battleState)
                    .OrderBy( (ent) => this.Position.DistanceTo(ent.Position) )
                    .FirstOrDefault();
                if (closestEnemy != null)
                    return TryBeginAttack(battleState, time, deltaSeconds, closestEnemy);
            }

            return null;
        }

        private void ChoosePath(BattleState battleState)
        {
            var path = battleState.FindPathToGoal(this.TeamId, this.Position);
            this.PlannedPath = new Queue<Vector2Di>(path);
        }

        private IEnumerable<BattleEntity> ListEnemiesInRange(BattleState battleState)
        {
            Func<BattleEntity,bool> isEnemy = (ent) => ent.TeamId != this.TeamId;
            Func<BattleEntity,bool> inRange = (ent) => this.Position.DistanceTo(ent.Position)<=this.Class.WeaponRangeTiles;
            Func<BattleEntity,bool> visible = (ent) => battleState.HasLineOfSight(this.Position, ent.Position);

            return battleState.GetAllEntities()
                .Where(isEnemy)
                .Where(inRange)
                .Where(visible);
        }
    }
}
