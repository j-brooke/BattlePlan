using System;
using System.Collections.Generic;

namespace BattlePlanEngine.Dto.V2
{
    public class BattleEvent
    {
        public double Time { get; set; }
        public string Type { get; set; }
        public string SourceEntity { get; set; }
        public int[] SourceLocation { get; set; }
        public int? SourceTeamId { get; set; }
        public string SourceClass { get; set; }
        public string TargetEntity { get; set; }
        public int[] TargetLocation { get; set; }
        public int? TargetTeamId { get; set; }
        public double? DamageAmount { get; set; }
    }
}
