using System;

namespace BattlePlanEngine.Model
{
    /// <summary>
    /// Characteristics of a unit's attacks.
    /// </summary>
    public enum WeaponType
    {
        None,

        /// <summary>
        /// Regular old swords and arrows.
        /// </summary>
        Physical,

        /// <summary>
        /// Spawns fire units on the ground along a line that persist for a short time.  It's
        /// possible they created in the same tile as enemies.  (It's the fire that then damages them,
        /// not the Flamestrike itself.)
        /// </summary>
        Flamestrike,

        /// <summary>
        /// Attack that damages one enemy, then jumps to another close-by one and damages them (a little
        /// less), and so on.
        /// </summary>
        ChainLightning,
    }
}
