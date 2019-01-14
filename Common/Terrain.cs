using System;
using System.Collections.Generic;

namespace BattlePlan.Common
{
    public class Terrain
    {
        public IList<TileCharacteristics> TileTypes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[][] Tiles { get; set; }

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

        public TileCharacteristics GetTile(int x, int y)
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

            if (TileTypes==null || typeIndex<0 || typeIndex>=TileTypes.Count)
                throw new ArgumentOutOfRangeException("Tile index out of range");

            return TileTypes[typeIndex];
        }
    }
}