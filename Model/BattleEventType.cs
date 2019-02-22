using System;

namespace BattlePlan.Model
{
    public enum BattleEventType
    {
        BeginMovement,
        EndMovement,
        BeginAttack,
        EndAttack,
        Spawn,
        Despawn,
        Die,
        ReachesGoal,
    }
}