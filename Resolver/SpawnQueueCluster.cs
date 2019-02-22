using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class for keeping track of planned attacker spawns, organized by teamId and spawn point index.
    /// </summary>
    internal class SpawnQueueCluster
    {
        public int Count { get; private set; }
        public IList<int> AttackerTeamIds { get; private set; }

        public SpawnQueueCluster(Terrain terrain, IEnumerable<AttackPlan> attackPlans)
        {
            var spawnPointTeamIds = terrain.SpawnPointsMap.Keys;
            var maxSpawnPointTeamId = spawnPointTeamIds.Any()? spawnPointTeamIds.Max() : 0;
            _grid = new Queue<AttackerSpawn>[maxSpawnPointTeamId][];

            // Set up a grid of queues arranged by teamId and spawnPointIndex, based on the terrain defs.
            foreach (var teamId in spawnPointTeamIds)
            {
                var teamQueues = new Queue<AttackerSpawn>[terrain.SpawnPointsMap[teamId].Count];
                _grid[teamId-1] = teamQueues;
                for (var i=0; i<teamQueues.Length; ++i)
                    teamQueues[i] = new Queue<AttackerSpawn>();
            }

            // Put every spawn command from the attack plans into the right queue, or throw an exception
            // if the team or spawn point doesn't exist.
            int spawnCount = 0;
            this.AttackerTeamIds = new List<int>();
            foreach (var plan in attackPlans)
            {
                if (plan.Spawns == null || plan.Spawns.Count == 0)
                    continue;

                if (plan.TeamId<1 || plan.TeamId>maxSpawnPointTeamId)
                    throw new InvalidScenarioException($"AttackPlan exists for team {plan.TeamId} but no spawn points are defined");

                var teamQueues = _grid[plan.TeamId-1];
                var sortedSpawns = plan.Spawns.OrderBy( (spawn) => spawn.Time );
                foreach (var spawnDef in sortedSpawns)
                {
                    if (spawnDef.SpawnPointIndex<0 || spawnDef.SpawnPointIndex>teamQueues.Length)
                        throw new InvalidScenarioException("Spawn list includes a SpawnPointIndex out of range");
                    teamQueues[spawnDef.SpawnPointIndex].Enqueue(spawnDef);
                    spawnCount += 1;
                }

                this.AttackerTeamIds.Add(plan.TeamId);
            }

            this.Count = spawnCount;
        }

        /// <summary>
        /// Returns the next spawn command for the given spawnPointIndex and teamId, if its
        /// spawn time is <= currentTime.
        /// </summary>
        public AttackerSpawn GetNext(int teamId, int spawnPointIndex, double currentTime)
        {
            var queue = _grid[teamId-1][spawnPointIndex];
            if (queue==null || queue.Count==0)
                return null;

            var nextSpawnDef = queue.Peek();
            if (nextSpawnDef.Time <= currentTime)
            {
                queue.Dequeue();
                this.Count -= 1;
                return nextSpawnDef;
            }
            else
            {
                return null;
            }
        }

        private Queue<AttackerSpawn>[][] _grid;
    }
}