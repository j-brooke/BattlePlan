using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Common;
using BattlePlan.Resolver;

namespace BattlePlan.MapGeneration
{
    /// <summary>
    /// Class for generating random maps.
    /// TODO: Might want to add a RNG seed to make the process repeatable.
    /// </summary>
    public class Generator
    {
        public Generator(GeneratorOptions options)
        {
            _options = options;
            InitTerrain();
            _rng = new Random();
        }

        public Terrain Create()
        {
            InitTerrain();

            // Cover the map with randomly sized rectangles of blocked ground.
            for (int pos=0; pos<_options.PositiveCycleCount; ++pos)
                ApplyRandomRect(1);

            // Create spawn points and goal points.
            for (int sp=0; sp<_options.SpawnPointCount; ++sp)
                CreateSpawnPoint();

            for (int g=0; g<_options.GoalCount; ++g)
                CreateGoalPoint();

            // If some of the spawn/goal points can't be reached from others,
            // apply random rectangles of passable ground.
            while (!IsEverythingConnected() && _options.SpawnPointCount>0)
            {
                for (int neg=0; neg<_options.NegativeCycleCount; ++neg)
                    ApplyRandomRect(0);
            }

            return _terrain;
        }

        // For now we'll assume only 1 attacking team.
        private const int _attackerTeamId = 1;

        private readonly GeneratorOptions _options;
        private Terrain _terrain;
        private Random _rng;

        private void InitTerrain()
        {
            _terrain = new Terrain()
            {
                Height = _options.Height,
                Width = _options.Width,
                TileTypes = new List<TileCharacteristics>()
                {
                    new TileCharacteristics() { BlocksMovement=false, BlocksVision=false, Appearance=" " },
                    new TileCharacteristics() { BlocksMovement=true, BlocksVision=true, Appearance=":" }
                }
            };

            _terrain.Tiles = new byte[_terrain.Height][];
            for (int row=0; row<_terrain.Height; ++row)
                _terrain.Tiles[row] = new byte[_terrain.Width];

            _terrain.SpawnPointsMap = new Dictionary<int,IList<Vector2Di>>() { { _attackerTeamId, new List<Vector2Di>() } };
            _terrain.GoalPointsMap = new Dictionary<int,IList<Vector2Di>>() { { _attackerTeamId, new List<Vector2Di>() } };
        }

        /// <summary>
        /// Sets the tiles in the specified rectangle to the given value.false  The specified
        /// region may be partly or wholely outside of the map without error.
        /// </summary>
        private void SetRect(int x, int y, int height, int width, byte value)
        {
            var boundedX = Math.Max(x, 0);
            var boundedY = Math.Max(y, 0);
            var boundedWidth = width - (x-boundedX);
            var boundedHeight = height - (y-boundedY);
            boundedWidth = Math.Min(boundedWidth, _terrain.Width-boundedX);
            boundedHeight = Math.Min(boundedHeight, _terrain.Height-boundedY);

            for (int r=0; r<boundedHeight; ++r)
                for (int c=0; c<boundedWidth; ++c)
                    _terrain.Tiles[r+boundedY][c+boundedX] = value;
        }

        private void ApplyRandomRect(byte value)
        {
            // Pick a random size and location.  We're allowing them to go off the map to allow more uniformity
            // around the edges.
            var blockWidth = _rng.Next(_options.ChunkSizeX)+1;
            var blockHeight = _rng.Next(_options.ChunkSizeY)+1;
            var x = _rng.Next(_options.ChunkSizeX + _options.Width + 1) - _options.ChunkSizeX;
            var y = _rng.Next(_options.ChunkSizeY + _options.Height + 1) - _options.ChunkSizeY;
            SetRect(x, y, blockHeight, blockWidth, value);
        }

        private void CreateSpawnPoint()
        {
            // For right now, we'll assume there's only 1 attacking team.
            var existingCount = _terrain.SpawnPointsMap[_attackerTeamId].Count;
            var segmentSize = _options.Height / (double)_options.SpawnPointCount;
            var y = (int)(existingCount * segmentSize + _rng.NextDouble() * segmentSize);
            var x = _rng.Next(_options.Width/5);
            var point = new Vector2Di(x,y);

            _terrain.SpawnPointsMap[_attackerTeamId].Add(point);
            SetRect(point.X-1, point.Y-1, 3, 3, 0);
        }

        private void CreateGoalPoint()
        {
            // For right now, we'll assume there's only 1 attacking team.
            var existingCount = _terrain.GoalPointsMap[_attackerTeamId].Count;
            var segmentSize = _options.Height / (double)_options.GoalCount;
            var y = (int)(existingCount * segmentSize + _rng.NextDouble() * segmentSize);
            var x = _options.Width - _rng.Next(_options.Width/5) - 1;
            var point = new Vector2Di(x,y);

            _terrain.GoalPointsMap[_attackerTeamId].Add(point);
            SetRect(point.X-1, point.Y-1, 3, 3, 0);
        }

        /// <summary>
        /// Tests whether all spawn points and goal points are reachable from eachother, across
        /// all teams.
        /// </summary>
        private bool IsEverythingConnected()
        {
            // Make a list of all spawn and goal points.
            var allSpawnPoints = _terrain.SpawnPointsMap.Values.SelectMany( (x) => x );
            var allGoalPoints = _terrain.GoalPointsMap.Values.SelectMany( (x) => x );
            var allPoints = allSpawnPoints.Union(allGoalPoints);

            if (!allPoints.Any())
                return false;

            // Make a set of all points reachable from one of the spawn/goal points.
            var graph = new BattlePathGraph() { Terrain = _terrain };
            var connectedPoints = graph.FindReachableSet(allPoints.First());

            // Test whether that set contains them all.
            return allPoints.All( (pt) => connectedPoints.Contains(pt) );
        }
    }
}