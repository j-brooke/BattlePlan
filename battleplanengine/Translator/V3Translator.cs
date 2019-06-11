using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlanEngine;

namespace BattlePlanEngine.Translator
{
    /// <summary>
    /// Class that provides translation between domain objects and the version 2 DTO schema.
    /// </summary>
    public static class V3Translator
    {
        public static readonly char separator = ',';

        public static Model.Vector2Di ToVectorModel(IList<int> dto)
        {
            var x = (dto!=null && dto.Count>=1)? dto[0] : 0;
            var y = (dto!=null && dto.Count>=2)? dto[1] : 0;
            return new Model.Vector2Di(x, y);
        }

        public static int[] ToVectorDto(Model.Vector2Di model)
        {
            return new int[] { model.X, model.Y };
        }

        public static T ToEnumModel<T>(string dto)
        {
            return (T)Enum.Parse(typeof(T), dto);
        }

        public static string ToEnumDto(Enum model)
        {
            return model.ToString();
        }

        public static Model.UnitCharacteristics ToModel(Dto.V3.UnitCharacteristics dto)
        {
            return new Model.UnitCharacteristics()
            {
                Name = dto.Name,
                Symbol = dto.Symbol,
                Behavior = ToEnumModel<Model.UnitBehavior>(dto.Behavior),
                CanBeAttacker = dto.CanBeAttacker,
                CanBeDefender = dto.CanBeDefender,
                Attackable = dto.Attackable,
                BlocksTile = dto.BlocksTile,
                SpeedTilesPerSec = dto.SpeedTilesPerSec,
                InitialHitPoints = dto.InitialHitPoints,
                WeaponType = ToEnumModel<Model.WeaponType>(dto.WeaponType),
                WeaponUseTime = dto.WeaponUseTime,
                WeaponReloadTime = dto.WeaponReloadTime,
                WeaponRangeTiles = dto.WeaponRangeTiles,
                WeaponDamage = dto.WeaponDamage,
                ResourceCost = dto.ResourceCost,
                CrowdAversionBias = dto.CrowdAversionBias,
                HurtAversionBias = dto.HurtAversionBias,
            };
        }
        public static Dto.V3.UnitCharacteristics ToDto(Model.UnitCharacteristics model)
        {
            return new Dto.V3.UnitCharacteristics()
            {
                Name = model.Name,
                Symbol = model.Symbol,
                Behavior = ToEnumDto(model.Behavior),
                CanBeAttacker = model.CanBeAttacker,
                CanBeDefender = model.CanBeDefender,
                Attackable = model.Attackable,
                BlocksTile = model.BlocksTile,
                SpeedTilesPerSec = model.SpeedTilesPerSec,
                InitialHitPoints = model.InitialHitPoints,
                WeaponType = ToEnumDto(model.WeaponType),
                WeaponUseTime = model.WeaponUseTime,
                WeaponReloadTime = model.WeaponReloadTime,
                WeaponRangeTiles = model.WeaponRangeTiles,
                WeaponDamage = model.WeaponDamage,
                ResourceCost = model.ResourceCost,
                CrowdAversionBias = model.CrowdAversionBias,
                HurtAversionBias = model.HurtAversionBias,
            };
        }

        public static Model.TileCharacteristics ToModel(Dto.V3.TileCharacteristics dto)
        {
            return new Model.TileCharacteristics()
            {
                BlocksMovement = dto.BlocksMovement,
                BlocksVision = dto.BlocksVision,
                Appearance = dto.Appearance,
                Name = dto.Name,
            };
        }
        public static Dto.V3.TileCharacteristics ToDto(Model.TileCharacteristics model)
        {
            return new Dto.V3.TileCharacteristics()
            {
                BlocksMovement = model.BlocksMovement,
                BlocksVision = model.BlocksVision,
                Appearance = model.Appearance,
                Name = model.Name,
            };
        }

        public static Model.DefenderPlacement ToModel(Dto.V3.DefenderPlacement dto)
        {
            return new Model.DefenderPlacement()
            {
                UnitType = dto.UnitType,
                Position = ToVectorModel(dto.Position),
            };
        }

        public static Dto.V3.DefenderPlacement ToDto(Model.DefenderPlacement model)
        {
            return new Dto.V3.DefenderPlacement()
            {
                UnitType = model.UnitType,
                Position = ToVectorDto(model.Position),
            };
        }

        public static Model.AttackerSpawn ToModel(Dto.V3.AttackerSpawn dto)
        {
            return new Model.AttackerSpawn()
            {
                Time = dto.Time,
                UnitType = dto.UnitType,
                SpawnPointIndex = dto.SpawnPointIndex,
            };
        }

        public static Dto.V3.AttackerSpawn ToDto(Model.AttackerSpawn model)
        {
            return new Dto.V3.AttackerSpawn()
            {
                Time = model.Time,
                UnitType = model.UnitType,
                SpawnPointIndex = model.SpawnPointIndex,
            };
        }

        public static Model.BattleEvent ToModel(string dto)
        {
            // Deserialize a BattleEvent from a comma-separated string.  (This is done to save space.)
            var arr = dto.Split(separator);

            var evt = new Model.BattleEvent();
            evt.Time = double.Parse(arr[0]);
            evt.Type = ToEnumModel<Model.BattleEventType>(arr[1]);
            evt.SourceEntity = (arr[2].Length>0)? int.Parse(arr[2]) : -1;
            evt.SourceLocation = (arr[3].Length>0 && arr[4].Length>0)?
                new Model.Vector2Di(int.Parse(arr[3]), int.Parse(arr[4]))
                : new Nullable<Model.Vector2Di>();
            evt.SourceTeamId = int.Parse(arr[5]);
            evt.SourceClass = arr[6];
            evt.TargetEntity = (arr[7].Length>0)? int.Parse(arr[7]) : -1;
            evt.TargetLocation = (arr[8].Length>0 && arr[9].Length>0)?
                new Model.Vector2Di(int.Parse(arr[8]), int.Parse(arr[9]))
                : new Nullable<Model.Vector2Di>();
            evt.TargetTeamId = int.Parse(arr[10]);
            evt.DamageAmount = (arr[11].Length>0)? double.Parse(arr[11]) : new Nullable<double>();

            return evt;
        }

        public static string ToDto(Model.BattleEvent model)
        {
            // Serialize BattleEvents into comma-separated strings to save space.  Each BattleResolution will have
            // a huge number of events, and repeating the JSON structure for each of those is just too wasteful.
            var arr = new object[12];
            arr[0] = model.Time.ToString("F3");
            arr[1] = ToEnumDto(model.Type);
            arr[2] = model.SourceEntity;
            arr[3] = model.SourceLocation?.X;
            arr[4] = model.SourceLocation?.Y;
            arr[5] = model.SourceTeamId;
            arr[6] = model.SourceClass;
            arr[7] = model.TargetEntity;
            arr[8] = model.TargetLocation?.X;
            arr[9] = model.TargetLocation?.Y;
            arr[10] = model.TargetTeamId;
            arr[11] = model.DamageAmount;

            return string.Join(separator.ToString(), arr);
        }

        public static Model.AttackPlan ToModel(Dto.V3.AttackPlan dto)
        {
            return new Model.AttackPlan()
            {
                TeamId = dto.TeamId,
                Spawns = dto.Spawns.Select( (item) => ToModel(item) ).ToList(),
            };
        }

        public static Dto.V3.AttackPlan ToDto(Model.AttackPlan model)
        {
            return new Dto.V3.AttackPlan()
            {
                TeamId = model.TeamId,
                Spawns = model.Spawns.Select( (item) => ToDto(item) ).ToList(),
            };
        }

        public static Model.DefensePlan ToModel(Dto.V3.DefensePlan dto)
        {
            return new Model.DefensePlan()
            {
                TeamId = dto.TeamId,
                Placements = dto.Placements.Select( (item) => ToModel(item) ).ToList(),
            };
        }

        public static Dto.V3.DefensePlan ToDto(Model.DefensePlan model)
        {
            return new Dto.V3.DefensePlan()
            {
                TeamId = model.TeamId,
                Placements = model.Placements.Select( (item) => ToDto(item) ).ToList(),
            };
        }

        public static Model.DefenderChallenge ToModel(Dto.V3.DefenderChallenge dto)
        {
            return new Model.DefenderChallenge()
            {
                Name = dto.Name,
                PlayerTeamId = dto.PlayerTeamId,
                MinimumDistFromSpawnPts = dto.MinimumDistFromSpawnPts,
                MinimumDistFromGoalPts = dto.MinimumDistFromGoalPts,
                MaximumResourceCost = dto.MaximumResourceCost,
                MaximumTotalUnitCount = dto.MaximumTotalUnitCount,
                MaximumDefendersLostCount = dto.MaximumDefendersLostCount,
                AttackersMustNotReachGoal = dto.AttackersMustNotReachGoal,
                MaximumUnitTypeCount = new Dictionary<string,int>(dto.MaximumUnitTypeCount),
            };
        }

        public static Dto.V3.DefenderChallenge ToDto(Model.DefenderChallenge model)
        {
            return new Dto.V3.DefenderChallenge()
            {
                Name = model.Name,
                PlayerTeamId = model.PlayerTeamId,
                MinimumDistFromSpawnPts = model.MinimumDistFromSpawnPts,
                MinimumDistFromGoalPts = model.MinimumDistFromGoalPts,
                MaximumResourceCost = model.MaximumResourceCost,
                MaximumTotalUnitCount = model.MaximumTotalUnitCount,
                MaximumDefendersLostCount = model.MaximumDefendersLostCount,
                AttackersMustNotReachGoal = model.AttackersMustNotReachGoal,
                MaximumUnitTypeCount = new Dictionary<string,int>(model.MaximumUnitTypeCount),
            };
        }

        public static Model.Terrain ToModel(Dto.V3.Terrain dto)
        {
            var spawns = new Dictionary<int,IList<Model.Vector2Di>>();
            if (dto.SpawnPointsMap != null)
                foreach (var teamId in dto.SpawnPointsMap.Keys)
                    spawns[teamId] = dto.SpawnPointsMap[teamId].Select( (item) => ToVectorModel(item) ).ToList();
            var goals = new Dictionary<int,IList<Model.Vector2Di>>();
            if (dto.GoalPointsMap != null)
                foreach (var teamId in dto.GoalPointsMap.Keys)
                    goals[teamId] = dto.GoalPointsMap[teamId].Select( (item) => ToVectorModel(item) ).ToList();

            var modelTileTypes = dto.TileTypes.Select( (item) => ToModel(item) ).ToList();
            var modelTerrain = new Model.Terrain(dto.Width, dto.Height, modelTileTypes)
            {
                SpawnPointsMap = spawns,
                GoalPointsMap = goals,
            };

            var tileSymbolMap = new Dictionary<char,int>();
            for (var i=0; i<dto.TileTypes.Count; ++i)
                tileSymbolMap[dto.TileTypes[i].Appearance] = i;

            for (var row=0; dto.Tiles!=null && row<dto.Tiles.Length; ++row)
            {
                var rowStr = dto.Tiles[row];
                for (var col=0; rowStr!=null && col<rowStr.Length && col<modelTerrain.Width; ++col)
                {
                    int tileValue = 0;
                    if (tileSymbolMap.TryGetValue(rowStr[col], out tileValue))
                        modelTerrain.SetTileValue(col, row, (byte)tileValue);
                }
            }

            return modelTerrain;
        }

        public static Dto.V3.Terrain ToDto(Model.Terrain model)
        {
            var spawns = new Dictionary<int,IList<int[]>>();
            if (model.SpawnPointsMap != null)
                foreach (var teamId in model.SpawnPointsMap.Keys)
                    spawns[teamId] = model.SpawnPointsMap[teamId].Select( (item) => ToVectorDto(item) ).ToList();
            var goals = new Dictionary<int,IList<int[]>>();
            if (model.GoalPointsMap != null)
                foreach (var teamId in model.GoalPointsMap.Keys)
                    goals[teamId] = model.GoalPointsMap[teamId].Select( (item) => ToVectorDto(item) ).ToList();

            var dtoTerrain = new Dto.V3.Terrain()
            {
                TileTypes = model.TileTypes.Select( (item) => ToDto(item) ).ToList(),
                Width = model.Width,
                Height = model.Height,
                SpawnPointsMap = spawns,
                GoalPointsMap = goals,
            };

            var dtoTiles = new string[model.Height];
            for (var row=0; row<model.Height; ++row)
            {
                var rowChars = new char[model.Width];
                for (var col=0; col<model.Width; ++col)
                    rowChars[col] = model.GetTile(col, row).Appearance;
                dtoTiles[row] = new string(rowChars);
            }

            dtoTerrain.Tiles = dtoTiles;
            return dtoTerrain;
        }

        public static Model.Scenario ToModel(Dto.V3.Scenario dto)
        {
            return new Model.Scenario()
            {
                BannerText = dto.BannerText.ToList(),
                Terrain = ToModel(dto.Terrain),
                AttackPlans = dto.AttackPlans.Select( (item) => ToModel(item) ).ToList(),
                DefensePlans = dto.DefensePlans.Select( (item) => ToModel(item) ).ToList(),
                Challenges = dto.Challenges.Select( (item) => ToModel(item) ).ToList()
            };
        }

        public static Dto.V3.Scenario ToDto(Model.Scenario model)
        {
            return new Dto.V3.Scenario()
            {
                BannerText = model.BannerText.ToList(),
                Terrain = ToDto(model.Terrain),
                AttackPlans = model.AttackPlans.Select( (item) => ToDto(item) ).ToList(),
                DefensePlans = model.DefensePlans.Select( (item) => ToDto(item) ).ToList(),
                Challenges = model.Challenges.Select( (item) => ToDto(item) ).ToList()
            };
        }

        public static Model.BattleResolution ToModel(Dto.V3.BattleResolution dto)
        {
            return new Model.BattleResolution()
            {
                BannerText = dto.BannerText?.ToList(),
                Terrain = ToModel(dto.Terrain),
                UnitTypes = dto.UnitTypes.Select( (item) => ToModel(item) ).ToList(),
                Events = dto.Events?.Select( (item) => ToModel(item) ).ToList(),
                AttackerBreachCounts = new Dictionary<int,int>(dto.AttackerBreachCounts),
                DefenderCasualtyCounts = new Dictionary<int,int>(dto.DefenderCasualtyCounts),
                ChallengesAchieved = dto.ChallengesAchieved?.Select( (item) => ToModel(item) ).ToList(),
                ChallengesFailed = dto.ChallengesFailed?.Select( (item) => ToModel(item) ).ToList(),
                AttackerResourceTotals = new Dictionary<int,int>(dto.AttackerResourceTotals),
                DefenderResourceTotals = new Dictionary<int,int>(dto.DefenderResourceTotals),
                ErrorMessages = dto.ErrorMessages?.ToList(),
            };
        }

        public static Dto.V3.BattleResolution ToDto(Model.BattleResolution model)
        {
            return new Dto.V3.BattleResolution()
            {
                BannerText = model.BannerText?.ToList(),
                Terrain = ToDto(model.Terrain),
                UnitTypes = model.UnitTypes?.Select( (item) => ToDto(item) ).ToList(),
                Events = model.Events?.Select( (item) => ToDto(item) ).ToList(),
                AttackerBreachCounts = new Dictionary<int,int>(model.AttackerBreachCounts),
                DefenderCasualtyCounts = new Dictionary<int,int>(model.DefenderCasualtyCounts),
                ChallengesAchieved = model.ChallengesAchieved?.Select( (item) => ToDto(item) ).ToList(),
                ChallengesFailed = model.ChallengesFailed?.Select( (item) => ToDto(item) ).ToList(),
                AttackerResourceTotals = new Dictionary<int,int>(model.AttackerResourceTotals),
                DefenderResourceTotals = new Dictionary<int,int>(model.DefenderResourceTotals),
                ErrorMessages = model.ErrorMessages?.ToList(),
            };
        }

    }
}
