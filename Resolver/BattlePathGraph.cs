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
        public Terrain Terrain { get; set; }

        public double Cost(Vector2Di fromNode, Vector2Di toNode)
        {
            if (toNode.Equals(_afterGoalNode))
            {
                // FIXME - make it work for any team, not hard-coded to 1.
                if (this.Terrain.GoalPointsMap[1].Contains(fromNode))
                    return 0;
                else
                    return double.PositiveInfinity;
            }

            var deltaX = Math.Abs(fromNode.X - toNode.X);
            var deltaY = Math.Abs(fromNode.Y - toNode.Y);

            switch (deltaX+deltaY)
            {
                case 0:
                    return 0.0;
                case 1:
                    return 1.0;
                case 2:
                    return _sqrt2;
                default:
                    return double.PositiveInfinity;
            }
        }

        public double EstimatedDistance(Vector2Di fromNode, Vector2Di toNode)
        {
            if (toNode.Equals(_afterGoalNode))
                return 0.0;

            var dist = this.Terrain.GoalPointsMap[1]
                .Select( (goalNode) => DiagonalDistance(fromNode, goalNode) )
                .Min();
            return dist;
        }

        public IEnumerable<Vector2Di> Neighbors(Vector2Di fromNode)
        {
            if (fromNode.Equals(_afterGoalNode))
                return Enumerable.Empty<Vector2Di>();

            if (fromNode.X<0 || fromNode.X>=this.Terrain.Width || fromNode.Y<0 || fromNode.Y>=this.Terrain.Height)
                throw new ArgumentOutOfRangeException("fromNode");

            bool openUp = fromNode.Y>0 && !this.Terrain.GetTile(fromNode.X, fromNode.Y-1).BlocksMovement;
            bool openDown = fromNode.Y<this.Terrain.Height-1 && !this.Terrain.GetTile(fromNode.X, fromNode.Y+1).BlocksMovement;
            bool openLeft = fromNode.X>0 && !this.Terrain.GetTile(fromNode.X-1, fromNode.Y).BlocksMovement;
            bool openRight = fromNode.X<this.Terrain.Width-1 && !this.Terrain.GetTile(fromNode.X+1, fromNode.Y).BlocksMovement;

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
            if (openUp & openLeft && !this.Terrain.GetTile(fromNode.X-1, fromNode.Y-1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X-1, fromNode.Y-1));
            if (openUp & openRight && !this.Terrain.GetTile(fromNode.X+1, fromNode.Y-1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X+1, fromNode.Y-1));
            if (openDown & openLeft && !this.Terrain.GetTile(fromNode.X-1, fromNode.Y+1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X-1, fromNode.Y+1));
            if (openDown & openRight && !this.Terrain.GetTile(fromNode.X+1, fromNode.Y+1).BlocksMovement)
                list.Add(new Vector2Di(fromNode.X+1, fromNode.Y+1));

            // Special non-Euclidean neighbor.  The algorithm can't directly handle multiple goals, so
            // we use _afterGoalNode as the destination as far as A* is concerned, and make it a zero-
            // cost neighbor of all of our real goal nodes.
            if (_goals!=null && _goals.Contains(fromNode))
                list.Add(_afterGoalNode);

            return list;
        }

        public IList<Vector2Di> FindPathToGoal(int teamId, Vector2Di startPos)
        {
            _goals = this.Terrain.GoalPointsMap[teamId];
            var path = BattlePlan.Path.AStar.FindPath(this, startPos, _afterGoalNode);

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

        private static double DiagonalDistance(Vector2Di fromNode, Vector2Di toNode)
        {
            var deltaX = Math.Abs(fromNode.X - toNode.X);
            var deltaY = Math.Abs(fromNode.Y - toNode.Y);

            return (deltaX + deltaY) - (2-_sqrt2) * Math.Min(deltaX, deltaY);
        }

    }
}
