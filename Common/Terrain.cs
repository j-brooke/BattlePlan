using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Terrain
    {
        public IList<TileCharacteristics> TileTypes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[][] Tiles { get { return _tiles; } set { _tiles = value; } }

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

        public static Terrain NewDefault()
        {
            return new Terrain()
                {
                    Width = 50,
                    Height = 36,
                    TileTypes = new List<TileCharacteristics>()
                {
                    new TileCharacteristics() { BlocksMovement=false, BlocksVision=false, Appearance=" ", Name="Open" },
                    new TileCharacteristics() { BlocksMovement=true, BlocksVision=true, Appearance=":", Name="Wall" },
                    new TileCharacteristics() { BlocksMovement=true, BlocksVision=false, Appearance="~", Name="Water" },
                    new TileCharacteristics() { BlocksMovement=false, BlocksVision=true, Appearance="@", Name="Fog" },
                },
            };
        }

        public TileCharacteristics GetTile(int x, int y)
        {
            var typeIndex = GetTileValue(x, y);
            if (TileTypes==null || typeIndex<0 || typeIndex>=TileTypes.Count)
                throw new ArgumentOutOfRangeException("Tile index out of range");

            return TileTypes[typeIndex];
        }

        public byte GetTileValue(int x, int y)
        {
            if (x<0 || x>=Width)
                throw new ArgumentOutOfRangeException("x");
            if (y<0 || y>=Height)
                throw new ArgumentOutOfRangeException("y");

            byte typeIndex = 0;
            if (Tiles!=null && Tiles.Length > y)
            {
                var row = Tiles[y];
                if (row!=null && row.Length > x)
                    typeIndex = row[x];
            }

            return typeIndex;
        }

        public void SetTileValue(int x, int y, byte val)
        {
            if (x<0 || x>=this.Width)
                throw new ArgumentOutOfRangeException("x");
            if (y<0 || y>=this.Height)
                throw new ArgumentOutOfRangeException("y");

            if (_tiles==null)
                _tiles = new byte[this.Height][];
            else if (_tiles.Length!=this.Height)
                Array.Resize(ref _tiles, this.Height);

            if (_tiles[y]==null)
                _tiles[y] = new byte[this.Width];
            else if (_tiles[y].Length!=this.Width)
                Array.Resize(ref _tiles[y], this.Width);

            _tiles[y][x] = val;
        }


        public bool HasLineOfSight(Vector2Di fromPos, Vector2Di toPos)
        {
            // Quick and dirty ray-casting algorithm.  This might be adequate for our needs, but
            // see this blog post for a discussion of different algorithms and their properties:
            //   http://www.adammil.net/blog/v125_Roguelike_Vision_Algorithms.html#raycode

            var dX = toPos.X - fromPos.X;
            var dY = toPos.Y - fromPos.Y;
            var absDX = Math.Abs(dX);
            var absDY = Math.Abs(dY);

            // A unit can always see into its tile, or an adjacent one.
            if (absDX<=1 & absDY<=1)
                return true;

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
                    if (GetTile(lineX, lineY).BlocksVision)
                        return false;
                }
                while (lineX != toPos.X);
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
                    if (GetTile(lineX, lineY).BlocksVision)
                        return false;
                }
                while (lineY != toPos.Y);
            }

            return true;
        }

        private byte[][] _tiles;
    }
}