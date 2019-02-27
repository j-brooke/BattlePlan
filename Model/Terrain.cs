using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Model
{
    public class Terrain
    {
        public IList<TileCharacteristics> TileTypes { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Dictionary where the key is a TeamID and the value is a list of spawn points
        /// for that team.
        /// </summary>
        public IDictionary<int,IList<Vector2Di>> SpawnPointsMap { get; set; } = new Dictionary<int,IList<Vector2Di>>();

        /// <summary>
        /// Dictionary where the key is a TeamID and the value is a list of goal points
        /// for that team.
        /// </summary>
        public IDictionary<int,IList<Vector2Di>> GoalPointsMap { get; set; } = new Dictionary<int,IList<Vector2Di>>();

        public Terrain()
            : this(50, 22, null)
        { }

        public Terrain(int width, int height, IList<TileCharacteristics> tileTypes)
        {
            if (width<0 || width>_maxSaneSize)
                throw new ArgumentException("Bad terrain width", "width");
            if (height<0 || height>_maxSaneSize)
                throw new ArgumentException("Bad terrain height", "height");

            this.Width = width;
            this.Height = height;

            if (tileTypes != null)
            {
                if (tileTypes.Count<2)
                    throw new ArgumentException("Too few entries in tileTypes list");

                this.TileTypes = tileTypes.ToList().AsReadOnly();
            }
            else
            {
                this.TileTypes = new List<TileCharacteristics>()
                {
                    new TileCharacteristics() { BlocksMovement=false, BlocksVision=false, Appearance=' ', Name="Open" },
                    new TileCharacteristics() { BlocksMovement=true, BlocksVision=true, Appearance=':', Name="Stone" },
                    new TileCharacteristics() { BlocksMovement=true, BlocksVision=false, Appearance='~', Name="Water" },
                    new TileCharacteristics() { BlocksMovement=false, BlocksVision=true, Appearance='@', Name="Fog" },
                }.AsReadOnly();
            }

            _tiles = new byte[width, height];
        }

        public bool IsInBounds(Vector2Di pos)
        {
            return pos.X>=0 && pos.Y>=0 && pos.X<this.Width && pos.Y<this.Height;
        }

        public TileCharacteristics GetTile(int x, int y)
        {
            var typeIndex = GetTileValue(x, y);
            return TileTypes[typeIndex];
        }

        public TileCharacteristics GetTile(Vector2Di pos)
        {
            return GetTile(pos.X, pos.Y);
        }

        public byte GetTileValue(int x, int y)
        {
            if (x<0 || x>=Width)
                throw new ArgumentOutOfRangeException("x");
            if (y<0 || y>=Height)
                throw new ArgumentOutOfRangeException("y");

            return _tiles[x, y];
        }

        public byte GetTileValue(Vector2Di pos)
        {
            return GetTileValue(pos.X, pos.Y);
        }

        public void SetTileValue(int x, int y, byte val)
        {
            if (x<0 || x>=this.Width)
                throw new ArgumentOutOfRangeException("x");
            if (y<0 || y>=this.Height)
                throw new ArgumentOutOfRangeException("y");
            if (val<0 || val>=this.TileTypes.Count)
                throw new ArgumentOutOfRangeException("val");

            _tiles[x, y] = val;
        }

        public void SetTileValue(Vector2Di pos, byte val)
        {
            SetTileValue(pos.X, pos.Y, val);
        }

        public void ClearAllTiles()
        {
            Array.Clear(_tiles, 0, _tiles.Length);
        }

        /// <summary>
        /// Tests whether one tile can see to another: that is, whether there are any tiles
        /// with the BlocksVision property alone a straight-line path between them.
        /// A tile may always see itself and its adjacent ones.
        /// </summary>
        public bool HasLineOfSight(Vector2Di fromPos, Vector2Di toPos)
        {
            // Quick and dirty ray-casting algorithm.  This might be adequate for our needs, but
            // see this blog post for a discussion of different algorithms and their properties:
            //   http://www.adammil.net/blog/v125_Roguelike_Vision_Algorithms.html#raycode

            var absDX = Math.Abs(toPos.X - fromPos.X);
            var absDY = Math.Abs(toPos.Y - fromPos.Y);

            // A unit can always see into its tile, or an adjacent one.
            if (absDX<=1 & absDY<=1)
                return true;

            var path = StraightLinePath(fromPos, toPos);
            foreach (var stepPos in path)
            {
                if (GetTile(stepPos.X, stepPos.Y).BlocksVision)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a straight traversable path between two points, if one exists, or null.
        /// TODO: Move this somewhere else.  It doesn't belong in Terrain, since it involves not just
        /// tile characteristics but also unit movement behavior.
        /// </summary>
        public IList<Vector2Di> StraightWalkablePath(Vector2Di fromPos, Vector2Di toPos)
        {
            var straightPath = StraightLinePath(fromPos, toPos);
            var previousPos = fromPos;
            foreach (var pos in straightPath)
            {
                var tileChar = GetTile(pos.X, pos.Y);
                if (tileChar.BlocksVision || tileChar.BlocksMovement)
                    return null;

                // If this would be a diagonal move, make sure we're not cutting corners.
                // We only allow diagnonal movement if both of the corners we have to pass
                // are open.
                var dX = pos.X - previousPos.X;
                var dY = pos.Y - previousPos.Y;
                if (dX!=0 & dY!=0)
                {
                    if (GetTile(previousPos.X+dX, previousPos.Y).BlocksMovement)
                        return null;
                    if (GetTile(previousPos.X, previousPos.Y+dY).BlocksMovement)
                        return null;
                }

                previousPos = pos;
            }

            return straightPath.ToList();
        }

        /// <summary>
        /// Returns a sequence of positions representing a straight ray-cast line between them.
        /// fromPos is not included in the results but toPos is.  (Or, empty collection if they're
        /// the same.)  This is just math - it doesn't look at the underlying tile characteristics.
        /// If minDistance is greater than the natural distance between the two, the line continues.
        /// </summary>
        public IEnumerable<Vector2Di> StraightLinePath(Vector2Di fromPos, Vector2Di toPos, double minDistance = 0.0)
        {
            var dX = toPos.X - fromPos.X;
            var dY = toPos.Y - fromPos.Y;
            var absDX = Math.Abs(dX);
            var absDY = Math.Abs(dY);
            var minDistSquared = minDistance * minDistance;
            double distSquared;
            bool hitToPos = false;

            if (fromPos==toPos)
                yield break;

            // Figure out if we're going mostly up-down or mostly left-right.  We want the axis that changes
            // more rapidly to be our independent axis.
            if (absDX > absDY)
            {
                // For each integer X value, only look at the closest integer Y value alone the line.
                short incX = (short)Math.Sign(dX);
                double slope = (double)dY / (double)dX;
                short lineX = fromPos.X;
                do
                {
                    lineX += incX;
                    double lineYfloat = (lineX-fromPos.X)*slope + fromPos.Y;
                    short lineY = (short)Math.Round(lineYfloat);
                    distSquared = (fromPos.X - lineX)*(fromPos.X - lineX) + (fromPos.Y - lineY)*(fromPos.Y - lineY);
                    hitToPos |= lineX == toPos.X;
                    yield return new Vector2Di(lineX, lineY);
                }
                while (!hitToPos || distSquared<minDistSquared);
            }
            else
            {
                // Same as above, except Y is the independent variable instead of X.
                short incY = (short)Math.Sign(dY);
                double slope = (double)dX / (double)dY;
                short lineY = fromPos.Y;
                do
                {
                    lineY += incY;
                    double lineXfloat = (lineY-fromPos.Y)*slope + fromPos.X;
                    short lineX = (short)Math.Round(lineXfloat);
                    distSquared = (fromPos.X - lineX)*(fromPos.X - lineX) + (fromPos.Y - lineY)*(fromPos.Y - lineY);
                    hitToPos |= lineY == toPos.Y;
                    yield return new Vector2Di(lineX, lineY);
                }
                while (!hitToPos || distSquared<minDistSquared);
            }
        }

        private const int _maxSaneSize = 250;
        private readonly byte[,] _tiles;
    }
}