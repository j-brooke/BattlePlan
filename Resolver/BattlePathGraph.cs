using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using BattlePlan.Model;
using BattlePlan.Path;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that provides graph-theory type answers to the pathfinding algorithm based on the 2D tile map
    /// used by the game.
    ///
    /// TODO: Given how coupled this is with BattleState, maybe it shouldn't be a separate class.
    /// </summary>
    internal class BattlePathGraph : IPathGraph<Vector2Di, BattleEntity>
    {
        public BattlePathGraph(BattleState battleState)
        {
            _battleState = battleState;
            _terrain = battleState.Terrain;

            // Set up a reusable PathSolver.
            var spawns = _terrain.SpawnPointsMap.SelectMany( (kvp) => kvp.Value );
            var goals = _terrain.SpawnPointsMap.SelectMany( (kvp) => kvp.Value );
            var spawnsAndGoals = spawns.Concat(goals);
            _pathSolver = new PathSolver<Vector2Di, BattleEntity>(this);
            _pathSolver.BuildAdjacencyGraph(spawnsAndGoals);
        }

        public BattlePathGraph(Terrain terrain)
        {
            _battleState = null;
            _terrain = terrain;
        }

        /// <summary>
        /// Returns the cost, for pathfinding purposes, of moving between the given locations.
        /// The cost is mostly based on traversal time, but other factors may be mixed in.
        /// </summary>
        public double Cost(Vector2Di fromNode, Vector2Di toNode, BattleEntity entity)
        {
            // Don't bother penalizing friendly obstructions further away than this.  Odds are
            // whoever it is will have moved or died before we get there.
            const double crowdAversionRange = 8.0;

            var deltaAxisDist = Math.Abs(toNode.X-fromNode.X) + Math.Abs(toNode.Y-fromNode.Y);

            // In our game space, a path can only ever be from one tile to an adjacent one - no teleports.
            Debug.Assert(deltaAxisDist<=2);

            double distance = (deltaAxisDist==2)? _sqrt2 : deltaAxisDist;
            double timeToMove = distance/entity.SpeedTilesPerSec;

            double penalty = 0.0;

            // Possibly add penalties for things in the way.  We want units to look for routes around things
            // if it's not too much of a hassle, but wait or attack other times.
            if (_battleState != null && entity != null)
            {
                var blockingEnt = _battleState.GetEntityAt(toNode);
                if (blockingEnt != null)
                {
                    if (blockingEnt.TeamId == entity.TeamId)
                    {
                        if (blockingEnt.SpeedTilesPerSec<=0.0)
                        {
                            // If the blocking entity can't move, strongly incentivise this one to go around.
                            penalty = _blockedByFriendlyImmobileCost;
                        }
                        else
                        {
                            // If the blocking entity is mobile, make a penalty based on its speed and how far away
                            // it is.  We don't want to worry too much about distant obstacles.
                            if (entity.Position.DistanceTo(blockingEnt.Position)<crowdAversionRange)
                                penalty = entity.Class.CrowdAversionBias/blockingEnt.SpeedTilesPerSec;
                        }
                    }
                    else
                    {
                        // Make a penalty based on how long it'll take us to kill the thing in the way.  (This is
                        // an approximation using continuous math instead of discrete whacks, and it doesn't consider
                        // that our friends might be attacking too.)
                        if (entity.Class.WeaponDamage>0)
                            penalty = (blockingEnt.HitPoints / entity.Class.WeaponDamage)
                                * (entity.Class.WeaponUseTime + entity.Class.WeaponReloadTime);
                    }
                }
            }

            // Add a penalty for tiles where this entity will take damage. Normalize the DPS relative to the unit's health.
            // (Should this be it's starting HP instead?)
            var enemyDpsInTile = _battleState.HurtMap.GetHurtFactor(toNode, entity.TeamId);
            var myDeathPerSecond = enemyDpsInTile / entity.HitPoints;
            penalty += entity.Class.HurtAversionBias * myDeathPerSecond;

            return Math.Max(timeToMove + penalty, 0.0);
        }

        /// <summary>
        /// Returns an estimate of the cost of the path between two (potentially distant) points.
        /// This is what the A* algorithm often calls the "heuristic".
        /// </summary>
        public double EstimatedCost(Vector2Di fromNode, Vector2Di toNode, BattleEntity entity)
        {
            // Slight optimization.  A* sometimes needs to re-process nodes that it thought it was done with
            // if the heuristic can exceed the true cost.  Due to the quirks of floating point numbers,
            // the un-fudged calculation below was overshooting by 0.0000000000001 or so, requiring unnecessary
            // calculations and producing slightly suboptimal paths.
            const double fudgeFactor = 0.999;

            var dist = DiagonalDistance(fromNode, toNode);
            var time = fudgeFactor * dist/entity.SpeedTilesPerSec;
            return time;
        }

        public IEnumerable<Vector2Di> Neighbors(Vector2Di fromNode)
        {
            return MovementModel.ValidMovesFrom(_terrain, fromNode);
        }

        public IList<Vector2Di> FindPathToGoal(BattleState battleState, BattleEntity entity)
        {
            // If, somehow, we're asked to find the path for an immobile object, return null.
            if (entity.SpeedTilesPerSec <= 0.0)
                return null;

            var goals = _terrain.GoalPointsMap[entity.TeamId];
            var result = _pathSolver.FindPath(entity.Position, goals, entity);

            if (_logger.IsTraceEnabled)
                _logger.Trace(entity.Id + " - " + result.PerformanceSummary());

            return result.Path;
        }

        public IList<Vector2Di> FindPathToSomewhere(BattleState battleState, BattleEntity entity, IEnumerable<Vector2Di> destinations)
        {
            // If, somehow, we're asked to find the path for an immobile object, return null.
            if (entity.SpeedTilesPerSec <= 0.0)
                return null;

            var result = _pathSolver.FindPath(entity.Position, destinations, entity);

            if (_logger.IsTraceEnabled)
                _logger.Trace(entity.Id + " - " + result.PerformanceSummary());

            return result.Path;
        }

        public string DebugInfo()
        {
            double pctGraphUsed = 100.0 * _pathSolver.LifetimeNodesTouchedCount / (_pathSolver.GraphSize * _pathSolver.PathSolvedCount);
            double pctReprocessed = 100.0 * _pathSolver.LifetimeNodesReprocessedCount / (_pathSolver.GraphSize * _pathSolver.PathSolvedCount);

            var msg = string.Format("pathCount={0}; timeMS={1}; %nodesTouched={2:F2}; %nodesReprocessed={3:F2}; maxQueueSize={4}",
                _pathSolver.PathSolvedCount,
                _pathSolver.LifetimeSolutionTimeMS,
                pctGraphUsed,
                pctReprocessed,
                _pathSolver.LifetimeMaxQueueSize);
            return msg;
        }

        private const double _blockedByFriendlyImmobileCost = 1E+09;
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static double _sqrt2 = Math.Sqrt(2.0);
        private readonly BattleState _battleState;
        private readonly Terrain _terrain;
        private readonly PathSolver<Vector2Di,BattleEntity> _pathSolver;

        /// <summary>
        /// Distance between two points assuming you can move in 8 directions (axis-aligned and 45 degree angles).
        /// </summary>
        private static double DiagonalDistance(Vector2Di fromNode, Vector2Di toNode)
        {
            var deltaX = Math.Abs(fromNode.X - toNode.X);
            var deltaY = Math.Abs(fromNode.Y - toNode.Y);

            return (deltaX + deltaY) - (2-_sqrt2) * Math.Min(deltaX, deltaY);
        }
    }
}
