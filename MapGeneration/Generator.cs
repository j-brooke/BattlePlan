using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan.Model;
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

            // Lay down a couple rectangles of water.
            for (int i=0; i<_options.WaterRectCount; ++i)
                ApplyRandomRect(_waterType);

            // Add some random water dots adjacent to existing ones to grow the region in a less blocky way.
            if (_options.WaterDotCount>0 && HasAnyOfType(_waterType))
                for (int i=0; i<_options.WaterDotCount; ++i)
                    GrowDot(_waterType);

            // Cover the map with randomly sized rectangles of blocked ground.
            for (int i=0; i<_options.StoneRectCount; ++i)
                ApplyRandomRect(1);

            // Add some random stone dots adjacent to existing ones to grow the region in a less blocky way.
            if (_options.StoneDotCount>0 && HasAnyOfType(_stoneType))
                for (int i=0; i<_options.StoneDotCount; ++i)
                    GrowDot(_stoneType);

            // Create spawn points and goal points.
            for (int sp=0; sp<_options.SpawnPointCount; ++sp)
                CreateSpawnPoint();

            for (int g=0; g<_options.GoalCount; ++g)
                CreateGoalPoint();

            // If some of the spawn/goal points can't be reached from others,
            // apply random rectangles of passable ground.
            while (!IsEverythingConnected() && _options.SpawnPointCount>0)
            {
                for (int neg=0; neg<_options.OpenRectCount; ++neg)
                    ApplyRandomRect(_openType);
            }

            if (_rng.Next(100) < _options.FogChancePercent)
                AddFog();

            return _terrain;
        }

        // Hard-coded assumptions about terrain types.
        private const int _openType = 0;
        private const int _stoneType = 1;
        private const int _waterType = 2;
        private const int _fogType = 3;


        // Used to create a "score" for how much we want to fill in a randomly chosen tile to make
        // it like some of its neighbors.
        private static int[,] _neighborScoreGrid = new int[3,3] { {1, 2, 1}, {4, 0, 4}, {1, 2, 1} };
        private const int _neighborScoreGridTotal = 16;
        private const int _neighborScoreOffest = 3;

        // For now we'll assume only 1 attacking team.
        private const int _attackerTeamId = 1;

        private readonly GeneratorOptions _options;
        private Terrain _terrain;
        private Random _rng;

        private void InitTerrain()
        {
            _terrain = new Terrain(_options.Width, _options.Height, null);
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
                    _terrain.SetTileValue(c+boundedX, r+boundedY, value);
        }

        private void SetCircle(Vector2Di pos, double radius, byte setToValue, byte startingValue)
        {
            int radiusInt = (int)Math.Ceiling(Math.Max(0.0, radius));
            for (int y=pos.Y-radiusInt; y<=pos.Y+radiusInt; ++y)
            {
                for (int x=pos.X-radiusInt; x<=pos.X+radiusInt; ++x)
                {
                    if (x<0 || x>=_terrain.Width || y<0 || y>=_terrain.Height)
                        continue;

                    var iterPos = new Vector2Di(x, y);
                    if (iterPos.DistanceTo(pos)>radius)
                        continue;

                    if (_terrain.GetTileValue(iterPos)==startingValue)
                        _terrain.SetTileValue(iterPos, setToValue);
                }
            }
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
            var connectedPoints = MovementModel.FindReachableLocations(_terrain, allPoints.First());

            // Test whether that set contains them all.
            return allPoints.All( (pt) => connectedPoints.Contains(pt) );
        }

        private bool HasAnyOfType(byte value)
        {
            for (int y=0; y<_terrain.Height; ++y)
                for (int x=0; x<_terrain.Width; ++x)
                    if (_terrain.GetTileValue(x,y)==value)
                        return true;
            return false;
        }

        /// <summary>
        /// Picks a random spot.  If enough of the neighboring tiles have the value we're looking for,
        /// put that value there too.  The idea is to create more organic shapes than just rectangles.
        /// </summary>
        private void GrowDot(byte type)
        {
            var maxIters = _terrain.Height * _terrain.Width * 2;
            for (int i=0; i<maxIters; ++i)
            {
                var pos = RandomPosition();
                if (_terrain.GetTileValue(pos)==0)
                {
                    var neighborScore = GetNeighborScoreForVal(pos, type);
                    if (_rng.Next(_neighborScoreGridTotal-_neighborScoreOffest)<neighborScore-_neighborScoreOffest)
                    {
                        _terrain.SetTileValue(pos, type);
                        return;
                    }
                }
            }
        }

        private Vector2Di RandomPosition()
        {
            var x = _rng.Next(_terrain.Width);
            var y = _rng.Next(_terrain.Height);
            return new Vector2Di((short)x, (short)y);
        }

        /// <summary>
        /// Returns a score based on how many of the specified tile's 8 neighbors have a given value.
        /// The score counts diagonals for less (to make things less jagged) and horizonal neighbors
        /// more (to encourage wider blocks).
        /// </summary>
        private int GetNeighborScoreForVal(Vector2Di pos, byte value)
        {
            int count = 0;
            for (int dX=-1; dX<=1; ++dX)
            {
                for (int dY=-1; dY<=1; ++dY)
                {
                    var x = dX + pos.X;
                    var y = dY + pos.Y;
                    if (x>=0 && x<_terrain.Width && y>=0 && y<_terrain.Height)
                    {
                        var neighborVal = _terrain.GetTileValue(x, y);
                        if (neighborVal==value)
                            count += _neighborScoreGrid[dY+1, dX+1];
                    }
                }
            }
            return count;
        }

        private void AddFog()
        {
            var centerPos = RandomPosition();
            var numPatches = 6;

            for (int i=0; i<numPatches; ++i)
            {
                var patchX = centerPos.X - _options.ChunkSizeX + _rng.Next(2*_options.ChunkSizeX+1);
                var patchY = centerPos.Y - _options.ChunkSizeY + _rng.Next(2*_options.ChunkSizeY+1);
                var radius = _rng.NextDouble() * Math.Min(_options.ChunkSizeX, _options.ChunkSizeY);

                SetCircle(new Vector2Di(patchX, patchY), radius, _fogType, _openType);
            }
        }


    }
}