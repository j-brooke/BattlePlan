using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using BattlePlan.Common;
using BattlePlan.Path;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that provides graph-theory type answers to the pathfinding algorithm based on the 2D tile map
    /// used by the game.
    ///
    /// TODO: Given how coupled this is with BattleState, maybe it shouldn't be a separate class.
    /// </summary>
    internal class BattlePathGraph : IPathGraph<Vector2Di>
    {
        public BattlePathGraph(BattleState battleState)
        {
            _battleState = battleState;
            _terrain = battleState.Terrain;

            // Set up a reusable PathSolver.
            var spawns = _terrain.SpawnPointsMap.SelectMany( (kvp) => kvp.Value );
            var goals = _terrain.SpawnPointsMap.SelectMany( (kvp) => kvp.Value );
            var spawnsAndGoals = spawns.Concat(goals);
            _pathSolver = new PathSolver<Vector2Di>(this);
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
        public double Cost(Vector2Di fromNode, Vector2Di toNode)
        {
            // Don't bother penalizing friendly obstructions further away than this.  Odds are
            // whoever it is will have moved or died before we get there.
            const double crowdAversionRange = 8.0;

            var deltaAxisDist = Math.Abs(toNode.X-fromNode.X) + Math.Abs(toNode.Y-fromNode.Y);

            // In our game space, a path can only ever be from one tile to an adjacent one - no teleports.
            Debug.Assert(deltaAxisDist<=2);

            double distance = (deltaAxisDist==2)? _sqrt2 : deltaAxisDist;
            double timeToMove = distance/_unitSpeedTilesPerSecond;

            double penalty = 0.0;

            // Possibly add penalties for things in the way.  We want units to look for routes around things
            // if it's not too much of a hassle, but wait or attack other times.
            if (_battleState != null && _searchForEntity != null)
            {
                var blockingEnt = _battleState.GetEntityAt(toNode);
                if (blockingEnt != null)
                {
                    if (blockingEnt.TeamId == _searchForEntity.TeamId)
                    {
                        if (blockingEnt.SpeedTilesPerSec<=0.0)
                        {
                            // If the blocking entity can't move, strongly incentivise this one to go around.
                            penalty = double.PositiveInfinity;
                        }
                        else
                        {
                            // If the blocking entity is mobile, make a penalty based on its speed and how far away
                            // it is.  We don't want to worry too much about distant obstacles.
                            if (_searchForEntity.Position.DistanceTo(blockingEnt.Position)<crowdAversionRange)
                                penalty = _searchForEntity.Class.CrowdAversionBias/blockingEnt.SpeedTilesPerSec;
                        }
                    }
                    else
                    {
                        // Make a penalty based on how long it'll take us to kill the thing in the way.  (This is
                        // an approximation using continuous math instead of discrete whacks, and it doesn't consider
                        // that our friends might be attacking too.)
                        if (_searchForEntity.Class.WeaponDamage>0)
                            penalty = (blockingEnt.HitPoints / _searchForEntity.Class.WeaponDamage)
                                * (_searchForEntity.Class.WeaponUseTime + _searchForEntity.Class.WeaponReloadTime);
                    }
                }
            }

            // Add a penalty for tiles where this entity will take damage.
            var enemyDpsInTile = _battleState.HurtMap.GetHurtFactor(toNode, _searchForEntity.TeamId);
            {
                // Normalize the DPS relative to the unit's health.  (Should this be it's starting HP instead?)
                var myDeathPerSecond = enemyDpsInTile / _searchForEntity.HitPoints;
                penalty += _searchForEntity.Class.HurtAversionBias * myDeathPerSecond;

                // TODO: A possibly better approach would be to sum all the time-based penalties and time to move,
                // above, and then assess a hurt penalty based on the DPS and how long we expect to stay in the tile.
                // A barrier with an archer covering it should count for more than a separate barrier and archer at
                // different places on the path.  On the other hand, if you make things too smart, it takes the fun out.
                // Plus, this wouldn't allow for a unit to know how much team help it has.
            }

            return Math.Max(timeToMove + penalty, 0.0);
        }

        /// <summary>
        /// Returns an estimate of the cost of the path between two (potentially distant) points.
        /// This is what the A* algorithm often calls the "heuristic".
        /// </summary>
        public double EstimatedCost(Vector2Di fromNode, Vector2Di toNode)
        {
            var dist = DiagonalDistance(fromNode, toNode);
            var time = dist/_unitSpeedTilesPerSecond;
            return time;
        }

        public IEnumerable<Vector2Di> Neighbors(Vector2Di fromNode)
        {
            if (fromNode.X<0 || fromNode.X>=_terrain.Width || fromNode.Y<0 || fromNode.Y>=_terrain.Height)
                throw new ArgumentOutOfRangeException("fromNode");

            bool openUp = fromNode.Y>0 && !_terrain.GetTile(fromNode.X, fromNode.Y-1).BlocksMovement;
            bool openDown = fromNode.Y<_terrain.Height-1 && !_terrain.GetTile(fromNode.X, fromNode.Y+1).BlocksMovement;
            bool openLeft = fromNode.X>0 && !_terrain.GetTile(fromNode.X-1, fromNode.Y).BlocksMovement;
            bool openRight = fromNode.X<_terrain.Width-1 && !_terrain.GetTile(fromNode.X+1, fromNode.Y).BlocksMovement;

            var list = new List<Vector2Di>();
            if (openUp)
                yield return new Vector2Di(fromNode.X, fromNode.Y-1);
            if (openDown)
                yield return new Vector2Di(fromNode.X, fromNode.Y+1);
            if (openLeft)
                yield return new Vector2Di(fromNode.X-1, fromNode.Y);
            if (openRight)
                yield return new Vector2Di(fromNode.X+1, fromNode.Y);

            // Only allow diagonal movement if both cardinal directions are clear too.  I.e.,
            // don't allow cutting corners.
            if (openUp & openLeft && !_terrain.GetTile(fromNode.X-1, fromNode.Y-1).BlocksMovement)
                yield return new Vector2Di(fromNode.X-1, fromNode.Y-1);
            if (openUp & openRight && !_terrain.GetTile(fromNode.X+1, fromNode.Y-1).BlocksMovement)
                yield return new Vector2Di(fromNode.X+1, fromNode.Y-1);
            if (openDown & openLeft && !_terrain.GetTile(fromNode.X-1, fromNode.Y+1).BlocksMovement)
                yield return new Vector2Di(fromNode.X-1, fromNode.Y+1);
            if (openDown & openRight && !_terrain.GetTile(fromNode.X+1, fromNode.Y+1).BlocksMovement)
                yield return new Vector2Di(fromNode.X+1, fromNode.Y+1);
        }

        public IList<Vector2Di> FindPathToGoal(BattleState battleState, BattleEntity entity)
        {
            // If, somehow, we're asked to find the path for an immobile object, return null.
            if (entity.SpeedTilesPerSec <= 0.0)
                return null;

            // Hack.  Store data about the entity being moved and enemies and such.
            // TODO: Redesign this whole data structure.
            _unitSpeedTilesPerSecond = entity.SpeedTilesPerSec;
            _goals = _terrain.GoalPointsMap[entity.TeamId];
            _searchForEntity = entity;
            var result = _pathSolver.FindPath(entity.Position, _goals);

            if (_logger.IsTraceEnabled)
                _logger.Trace(entity.Id + " - " + result.PerformanceSummary());

            return result.Path;
        }

        /// <summary>
        /// Returns a collection of all locations reachable from startPos
        /// </summary>
        public ISet<Vector2Di> FindReachableSet(Vector2Di startPos)
        {
            var visited = new HashSet<Vector2Di>();
            var toCheck = new Queue<Vector2Di>();
            toCheck.Enqueue(startPos);

            while (toCheck.Count>0)
            {
                var pos = toCheck.Dequeue();
                if (!visited.Contains(pos))
                {
                    visited.Add(pos);
                    var neighbors = Neighbors(pos);
                    foreach (var node in neighbors)
                        toCheck.Enqueue(node);
                }
            }

            return visited;
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

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static double _sqrt2 = Math.Sqrt(2.0);

        private IList<Vector2Di> _goals = null;
        private BattleEntity _searchForEntity = null;

        private double _unitSpeedTilesPerSecond = 0.0;

        private readonly BattleState _battleState;
        private readonly Terrain _terrain;
        private readonly PathSolver<Vector2Di> _pathSolver;

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
