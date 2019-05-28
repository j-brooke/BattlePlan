using System;

namespace BattlePlanEngine.Model
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