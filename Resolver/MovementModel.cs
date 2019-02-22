using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;

namespace BattlePlan.Resolver
{
    /// <summary>
    /// Class that knows about how units can move on our map.
    /// </summary>
    public static class MovementModel
    {
        /// <summary>
        /// Returns an enumeration of all of the tiles to which a unit could move from the given start in one step.
        /// </summary>
        public static IEnumerable<Vector2Di> ValidMovesFrom(Terrain terrain, Vector2Di fromNode)
        {
            if (fromNode.X<0 || fromNode.X>=terrain.Width || fromNode.Y<0 || fromNode.Y>=terrain.Height)
                throw new ArgumentOutOfRangeException("fromNode");

            bool openUp = fromNode.Y>0 && !terrain.GetTile(fromNode.X, fromNode.Y-1).BlocksMovement;
            bool openDown = fromNode.Y<terrain.Height-1 && !terrain.GetTile(fromNode.X, fromNode.Y+1).BlocksMovement;
            bool openLeft = fromNode.X>0 && !terrain.GetTile(fromNode.X-1, fromNode.Y).BlocksMovement;
            bool openRight = fromNode.X<terrain.Width-1 && !terrain.GetTile(fromNode.X+1, fromNode.Y).BlocksMovement;

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
            if (openUp & openLeft && !terrain.GetTile(fromNode.X-1, fromNode.Y-1).BlocksMovement)
                yield return new Vector2Di(fromNode.X-1, fromNode.Y-1);
            if (openUp & openRight && !terrain.GetTile(fromNode.X+1, fromNode.Y-1).BlocksMovement)
                yield return new Vector2Di(fromNode.X+1, fromNode.Y-1);
            if (openDown & openLeft && !terrain.GetTile(fromNode.X-1, fromNode.Y+1).BlocksMovement)
                yield return new Vector2Di(fromNode.X-1, fromNode.Y+1);
            if (openDown & openRight && !terrain.GetTile(fromNode.X+1, fromNode.Y+1).BlocksMovement)
                yield return new Vector2Di(fromNode.X+1, fromNode.Y+1);
        }

        /// <summary>
        /// Returns a set of all a unit could reach from the given start location, in any number of steps.
        /// </summary>
        public static ISet<Vector2Di> FindReachableLocations(Terrain terrain, Vector2Di startPos)
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
                    var neighbors = ValidMovesFrom(terrain, pos);
                    foreach (var node in neighbors)
                        toCheck.Enqueue(node);
                }
            }

            return visited;
        }
    }
}