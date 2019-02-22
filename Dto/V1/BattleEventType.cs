using System;

namespace BattlePlan.Dto.V1
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