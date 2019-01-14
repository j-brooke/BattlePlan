using System;
using BattlePlan.Common;


namespace BattlePlan.Viewer
{
    internal class ViewEntity
    {
        public string Id { get; set; }
        public Vector2Di Position { get; set; }
        public int TeamId { get; set; }
        public UnitClass Class { get; set; }
        public char Symbol { get; set; }
    }
}
