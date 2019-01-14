using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Common
{
    public class AttackPlan
    {
        public int TeamId { get; set; }
        public List<AttackerSpawn> Spawns { get; set; }

        public AttackPlan()
        {
        }

        public AttackPlan(AttackPlan other)
        {
            this.TeamId = other.TeamId;
            if (other.Spawns!=null)
            {
                this.Spawns = new List<AttackerSpawn>();
                foreach (var spawn in other.Spawns)
                {
                    this.Spawns.Add(new AttackerSpawn(spawn));
                }
                this.Spawns = other.Spawns.Select( (spawn) => new AttackerSpawn(spawn) ).ToList();
            }
        }
    }
}