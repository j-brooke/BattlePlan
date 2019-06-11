using System;
using BattlePlanEngine.Model;


namespace BattlePlanConsole.Viewer
{
    internal class ViewEntity
    {
        public int Id { get; set; }
        public Vector2Di Position { get; set; }
        public int TeamId { get; set; }
        public UnitCharacteristics UnitType { get; set; }
        public char Symbol { get; set; }
    }
}
