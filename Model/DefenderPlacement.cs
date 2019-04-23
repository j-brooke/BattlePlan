using System;

namespace BattlePlan.Model
{
    /// <summary>
    /// Definition of where a particular defender unit is positioned.
    /// </summary>
    public class DefenderPlacement
    {
        public string UnitType { get; set; }
        public Vector2Di Position { get; set; }
    }
}