using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;
using BattlePlan.Path;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that provides graph-theory type answers to the pathfinding algorithm based on the 2D tile map
    /// used by the game.
    /// </summary>
    internal class BattlePathGraph : IPathGraph<Vector2Di>
    {
        public BattlePathGraph(BattleState battleState)
        {
            _battleState = battleState;
            _terrain = battleState.Terrain;
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
            if (toNode.Equals(_afterGoalNode))
            {
                if (_goals.Contains(fromNode))
                    return 0;
                else
                    return double.PositiveInfinity;
            }

            var delta = toNode - fromNode;
            double distance = Math.Sqrt(delta.X*delta.X + delta.Y*delta.Y);
            double timeToMove = distance/_unitSpeedTilesPerSecond;

            // Possibly add penalties for things in the way.  We want units to look for routes around things
            // if it's not too much of a hassle, but wait or attack other times.
            double penalty = 0.0;
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
                            // (Does this work?  Not sure how math goes with PositiveInfinity.)
                            penalty = double.PositiveInfinity;
                        }
                        else
                        {
                            // If the blocking entity is mobile, make a penalty based on its speed and how far away
                            // it is.  We don't want to worry too much about distant obstacles.
                            penalty = 2.0/blockingEnt.SpeedTilesPerSec/_searchForEntity.Position.DistanceTo(toNode);
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

            return timeToMove + penalty;
        }

        public double EstimatedDistance(Vector2Di fromNode, Vector2Di toNode)
        {
            if (toNode.Equals(_afterGoalNode))
                return 0.0;

            var dist = _terrain.GoalPointsMap[1]
                .Select( (goalNode) => DiagonalDistance(fromNode, goalNode) )
                .Min();
            return dist;
        }

        public IEnumerable<Vector2Di> Neighbors(Vector2Di fromNode)
        {
            if (fromNode.Equals(_afterGoalNode))
                return Enumerable.Empty<Vector2Di>();

            if (fromNode.X<0 || fromNode.X>=_terrain.Width || fromNode.Y<0 || fromNode.Y>=_terrain.Height)
                throw new ArgumentOutOfRangeException("fromNode");

            bool openUp = fromNode.Y>0 && !_terrain.GetTile(fromNode.X, fromNode.Y-1).BlocksMovement;
            bool openDown = fromNode.Y<_terrain.Height-1 && !_terrain.GetTile(fromNode.X, fromNode.Y+1).BlocksMovement;
            bool openLeft = fromNode.X>0 && !_terrain.GetTile(fromNode.X-1, fromNode.Y).BlocksMovement;
            bool openRight = fromNode.X<_terrain.Width-1 && !_terrain.GetTile(fromNode.X+1, fromNode.Y).BlocksMovement;

            var list = new List<Vector2Di>();
            if (openUp)
                list.Add(new Vector2Di(fromNode.X, fromNode.Y-1));
            if (openDown)
                list.Add(new Vector2Di(fromNode.X, fromNode.Y+1));
            if (openLeft)
                list.Add(new Vector2Di(fromNode.X-1, fromNode.Y));
            if (openRight)
                list.Add(new Vector2Di(fromNode.X+1, fromNode.Y));

            // Only allow diagonal movement if both cardinal directions are clear too.  I.e.,
            // don't allow cutting corners.
            if (openUp & openLeft && !_terrain.GetTile(fromNode.X-1, fromNode.Y-1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X-1, fromNode.Y-1));
            if (openUp & openRight && !_terrain.GetTile(fromNode.X+1, fromNode.Y-1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X+1, fromNode.Y-1));
            if (openDown & openLeft && !_terrain.GetTile(fromNode.X-1, fromNode.Y+1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X-1, fromNode.Y+1));
            if (openDown & openRight && !_terrain.GetTile(fromNode.X+1, fromNode.Y+1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X+1, fromNode.Y+1));

            // Special non-Euclidean neighbor.  The algorithm can't directly handle multiple goals, so
            // we use _afterGoalNode as the destination as far as A* is concerned, and make it a zero-
            // cost neighbor of all of our real goal nodes.
            if (_goals!=null && _goals.Contains(fromNode))
                list.Add(_afterGoalNode);

            return list;
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
            var path = BattlePlan.Path.AStar.FindPath(this, entity.Position, _afterGoalNode);

            // Remove _afterGoalNode since it's special and doesn't exist in our map space.
            if (path != null)
                path.RemoveAt(path.Count-1);

            return path;
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

        private static double _sqrt2 = Math.Sqrt(2.0);

        // Special non-Euclidean point used as a single destination to which all "real" destinations are connected.
        private static Vector2Di _afterGoalNode = new Vector2Di(-1, -1);

        private IList<Vector2Di> _goals = null;
        private BattleEntity _searchForEntity = null;

        private double _unitSpeedTilesPerSecond = 0.0;

        private readonly BattleState _battleState;
        private readonly Terrain _terrain;

        private static double DiagonalDistance(Vector2Di fromNode, Vector2Di toNode)
        {
            var deltaX = Math.Abs(fromNode.X - toNode.X);
            var deltaY = Math.Abs(fromNode.Y - toNode.Y);

            return (deltaX + deltaY) - (2-_sqrt2) * Math.Min(deltaX, deltaY);
        }

    }
}
