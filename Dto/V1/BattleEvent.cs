using System;
using System.Collections.Generic;

namespace BattlePlan.Dto.V1
{
    public class BattleEvent
    {
        public double Time { get; set; }
        public BattleEventType Type { get; set; }
        public string SourceEntity { get; set; }
        public Vector2Di? SourceLocation { get; set; }
        public int SourceTeamId { get; set; }
        public string SourceClass { get; set; }
        public string TargetEntity { get; set; }
        public Vector2Di? TargetLocation { get; set; }
        public int TargetTeamId { get; set; }
        public double? DamageAmount { get; set; }
    }
}
