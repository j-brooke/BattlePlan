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
        public IDictionary<int,IList<Vector2Di>> SpawnPointsMap { get; set; }

        /// <summary>
        /// Dictionary where the key is a TeamID and the value is a list of goal points
        /// for that team.
        /// </summary>
        public IDictionary<int,IList<Vector2Di>> GoalPointsMap { get; set; }

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

        private byte[][] _tiles;
    }
}