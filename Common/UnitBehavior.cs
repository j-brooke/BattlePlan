using System;

namespace BattlePlan.Common
{
    public enum UnitBehavior
    {
        /// <summary>
        /// Unit won't attack or move.  It's likely a Barricade, rock, or pacifist.
        /// </summary>
        None,

        /// <summary>
        /// Unit will always try to move toward its goal if possible, attacking only
        /// when there's another unit in its chosen path.
        /// </summary>
        Rusher,

        /// <summary>
        /// Unit will attack whenever it can, but otherwise will continue moving toward
        /// its goal.
        /// </summary>
        Marcher,

        /// <summary>
        /// Unit will charge and attack the closest enemy it sees.  Only when there are no
        /// enemies around will it advance toward its goal.
        /// </summary>
        Berserker,
    }
}
