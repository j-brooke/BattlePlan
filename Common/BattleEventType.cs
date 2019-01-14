using System;

namespace BattlePlan.Common
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