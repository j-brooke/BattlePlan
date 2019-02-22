using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Dto.V1
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

        private byte[][] _tiles;
    }
}