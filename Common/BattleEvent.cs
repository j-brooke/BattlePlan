using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class BattleEvent
    {
        public double Time { get; set; }
        public BattleEventType Type { get; set; }
        public string SourceEntity { get; set; }
        public Vector2Di? SourceLocation { get; set; }
        public int SourceTeamId { get; set; }
        public UnitClass? SourceClass { get; set; }
        public string TargetEntity { get; set; }
        public Vector2Di? TargetLocation { get; set; }
        public int TargetTeamId { get; set; }
        public double? DamageAmount { get; set; }

        public override string ToString()
        {
            var msg = $"{Time.ToString("F2")} {SourceEntity}";

            if (this.SourceLocation.HasValue)
                msg += $" at {SourceLocation.Value}";

            msg += $" {Type}";

            if (this.TargetEntity!=null || TargetLocation.HasValue)
                msg += $" toward {TargetEntity} at {TargetLocation}";

            if (this.DamageAmount.HasValue)
                msg+= $" for {DamageAmount.Value} damage";
            return msg;
        }
    }
}
