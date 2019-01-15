using System;
using BattlePlan.Common;


namespace BattlePlan.Viewer
{
    internal class ViewEntity
    {
        public string Id { get; set; }
        public Vector2Di Position { get; set; }
        public int TeamId { get; set; }
        public UnitCharacteristics UnitType { get; set; }
        public char Symbol { get; set; }
    }
}
