using System;
using System.Collections.Generic;

namespace BattlePlanEngine.Model
{
    /// <summary>
    /// Something that happens in a BattleResolution.  (Unit moves, takes damage, reaches goal, etc.)
    /// </summary>
    public class BattleEvent
    {
        public double Time { get; set; }
        public BattleEventType Type { get; set; }
        public int SourceEntity { get; set; }
        public Vector2Di? SourceLocation { get; set; }
        public int SourceTeamId { get; set; }
        public string SourceClass { get; set; }
        public int TargetEntity { get; set; }
        public Vector2Di? TargetLocation { get; set; }
        public int TargetTeamId { get; set; }
        public double? DamageAmount { get; set; }

        public override string ToString()
        {
            var msg = $"{Time.ToString("F2")} {SourceEntity}";

            if (this.SourceLocation.HasValue)
                msg += $" at {SourceLocation.Value}";

            msg += $" {Type}";

            if (this.TargetEntity>=0 || TargetLocation.HasValue)
                msg += $" toward {TargetEntity} at {TargetLocation}";

            if (this.DamageAmount.HasValue)
                msg+= $" for {DamageAmount.Value} damage";
            return msg;
        }
    }
}
