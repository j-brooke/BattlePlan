using System;
using System.Collections.Generic;
using System.Linq;

namespace BattlePlan.Dto.V2
{
    public class Terrain
    {
        public IList<TileCharacteristics> TileTypes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// Strings representing each row of the map.  The character at each position corresponds
        /// to the "Appearance" character in the TileTypes list.
        /// </summary>
        public string[] Tiles { get; set; }

        /// <summary>
        /// Dictionary where the key is a TeamID and the value is a list of spawn points
        /// for that team.
        /// </summary>
        public IDictionary<int,IList<int[]>> SpawnPointsMap { get; set; } = new Dictionary<int,IList<int[]>>();

        /// <summary>
        /// Dictionary where the key is a TeamID and the value is a list of goal points
        /// for that team.
        /// </summary>
        public IDictionary<int,IList<int[]>> GoalPointsMap { get; set; } = new Dictionary<int,IList<int[]>>();
    }
}
