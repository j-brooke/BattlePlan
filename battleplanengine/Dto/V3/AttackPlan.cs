using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlanEngine.Dto.V3
{
    public class AttackPlan
    {
        public int TeamId { get; set; }
        public IList<AttackerSpawn> Spawns { get; set; } = new List<AttackerSpawn>();
    }
}
