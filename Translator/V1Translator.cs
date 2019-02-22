using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlan;

namespace BattlePlan.Translator
{
    /// <summary>
    /// Class that provides translation between domain objects and the version 1 DTO schema.
    /// </summary>
    internal static class V1Translator
    {
        public static Model.Vector2Di ToModel(Dto.V1.Vector2Di dto)
        {
            return new Model.Vector2Di(dto.X, dto.Y);
        }

        public static Dto.V1.Vector2Di ToDto(Model.Vector2Di model)
        {
            return new Dto.V1.Vector2Di(model.X, model.Y);
        }

        public static Model.UnitBehavior ToModel(Dto.V1.UnitBehavior dto)
        {
            switch (dto)
            {
                case Dto.V1.UnitBehavior.None: return Model.UnitBehavior.None;
                case Dto.V1.UnitBehavior.Rusher: return Model.UnitBehavior.Rusher;
                case Dto.V1.UnitBehavior.Marcher: return Model.UnitBehavior.Marcher;
                case Dto.V1.UnitBehavior.Berserker: return Model.UnitBehavior.Berserker;
                default: throw new TranslatorException("Can't convert UnitBehavior");
            }
        }

        public static Dto.V1.UnitBehavior ToDto(Model.UnitBehavior dto)
        {
            switch (dto)
            {
                case Model.UnitBehavior.None: return Dto.V1.UnitBehavior.None;
                case Model.UnitBehavior.Rusher: return Dto.V1.UnitBehavior.Rusher;
                case Model.UnitBehavior.Marcher: return Dto.V1.UnitBehavior.Marcher;
                case Model.UnitBehavior.Berserker: return Dto.V1.UnitBehavior.Berserker;
                default: throw new TranslatorException("Can't convert UnitBehavior");
            }
        }

        public static Model.UnitCharacteristics ToModel(Dto.V1.UnitCharacteristics dto)
        {
            return new Model.UnitCharacteristics()
            {
                Name = dto.Name,
                Symbol = dto.Symbol,
                Behavior = ToModel(dto.Behavior),
                CanAttack = dto.CanAttack,
                CanDefend = dto.CanDefend,
                SpeedTilesPerSec = dto.SpeedTilesPerSec,
                InitialHitPoints = dto.InitialHitPoints,
                WeaponUseTime = dto.WeaponUseTime,
                WeaponReloadTime = dto.WeaponReloadTime,
                WeaponRangeTiles = dto.WeaponRangeTiles,
                WeaponDamage = dto.WeaponDamage,
                ResourceCost = dto.ResourceCost,
                CrowdAversionBias = dto.CrowdAversionBias,
                HurtAversionBias = dto.HurtAversionBias,
            };
        }
        public static Dto.V1.UnitCharacteristics ToDto(Model.UnitCharacteristics model)
        {
            return new Dto.V1.UnitCharacteristics()
            {
                Name = model.Name,
                Symbol = model.Symbol,
                Behavior = ToDto(model.Behavior),
                CanAttack = model.CanAttack,
                CanDefend = model.CanDefend,
                SpeedTilesPerSec = model.SpeedTilesPerSec,
                InitialHitPoints = model.InitialHitPoints,
                WeaponUseTime = model.WeaponUseTime,
                WeaponReloadTime = model.WeaponReloadTime,
                WeaponRangeTiles = model.WeaponRangeTiles,
                WeaponDamage = model.WeaponDamage,
                ResourceCost = model.ResourceCost,
                CrowdAversionBias = model.CrowdAversionBias,
                HurtAversionBias = model.HurtAversionBias,
            };
        }

        public static Model.TileCharacteristics ToModel(Dto.V1.TileCharacteristics dto)
        {
            return new Model.TileCharacteristics()
            {
                BlocksMovement = dto.BlocksMovement,
                BlocksVision = dto.BlocksVision,
                Appearance = dto.Appearance,
                Name = dto.Name,
            };
        }
        public static Dto.V1.TileCharacteristics ToDto(Model.TileCharacteristics model)
        {
            return new Dto.V1.TileCharacteristics()
            {
                BlocksMovement = model.BlocksMovement,
                BlocksVision = model.BlocksVision,
                Appearance = model.Appearance,
                Name = model.Name,
            };
        }

        public static Model.DefenderPlacement ToModel(Dto.V1.DefenderPlacement dto)
        {
            return new Model.DefenderPlacement()
            {
                UnitType = dto.UnitType,
                Position = ToModel(dto.Position),
            };
        }

        public static Dto.V1.DefenderPlacement ToDto(Model.DefenderPlacement model)
        {
            return new Dto.V1.DefenderPlacement()
            {
                UnitType = model.UnitType,
                Position = ToDto(model.Position),
            };
        }

        public static Model.AttackerSpawn ToModel(Dto.V1.AttackerSpawn dto)
        {
            return new Model.AttackerSpawn()
            {
                Time = dto.Time,
                UnitType = dto.UnitType,
                SpawnPointIndex = dto.SpawnPointIndex,
            };
        }

        public static Dto.V1.AttackerSpawn ToDto(Model.AttackerSpawn model)
        {
            return new Dto.V1.AttackerSpawn()
            {
                Time = model.Time,
                UnitType = model.UnitType,
                SpawnPointIndex = model.SpawnPointIndex,
            };
        }

        public static Model.BattleEventType ToModel(Dto.V1.BattleEventType dto)
        {
            switch (dto)
            {
                case Dto.V1.BattleEventType.BeginMovement: return Model.BattleEventType.BeginMovement;
                case Dto.V1.BattleEventType.EndMovement: return Model.BattleEventType.EndMovement;
                case Dto.V1.BattleEventType.BeginAttack: return Model.BattleEventType.BeginAttack;
                case Dto.V1.BattleEventType.EndAttack: return Model.BattleEventType.EndAttack;
                case Dto.V1.BattleEventType.Spawn: return Model.BattleEventType.Spawn;
                case Dto.V1.BattleEventType.Despawn: return Model.BattleEventType.Despawn;
                case Dto.V1.BattleEventType.Die: return Model.BattleEventType.Die;
                case Dto.V1.BattleEventType.ReachesGoal: return Model.BattleEventType.ReachesGoal;
                default: throw new TranslatorException("Can't convert BattleEventType");
            }
        }

        public static Dto.V1.BattleEventType ToDto(Model.BattleEventType dto)
        {
            switch (dto)
            {
                case Model.BattleEventType.BeginMovement: return Dto.V1.BattleEventType.BeginMovement;
                case Model.BattleEventType.EndMovement: return Dto.V1.BattleEventType.EndMovement;
                case Model.BattleEventType.BeginAttack: return Dto.V1.BattleEventType.BeginAttack;
                case Model.BattleEventType.EndAttack: return Dto.V1.BattleEventType.EndAttack;
                case Model.BattleEventType.Spawn: return Dto.V1.BattleEventType.Spawn;
                case Model.BattleEventType.Despawn: return Dto.V1.BattleEventType.Despawn;
                case Model.BattleEventType.Die: return Dto.V1.BattleEventType.Die;
                case Model.BattleEventType.ReachesGoal: return Dto.V1.BattleEventType.ReachesGoal;
                default: throw new TranslatorException("Can't convert BattleEventType");
            }
        }

        public static Model.BattleEvent ToModel(Dto.V1.BattleEvent dto)
        {
            return new Model.BattleEvent()
            {
                Time = dto.Time,
                Type = ToModel(dto.Type),
                SourceEntity = dto.SourceEntity,
                SourceLocation = (!dto.SourceLocation.HasValue)? new Nullable<Model.Vector2Di>() : ToModel(dto.SourceLocation.Value),
                SourceTeamId = dto.SourceTeamId,
                SourceClass = dto.SourceClass,
                TargetEntity = dto.TargetEntity,
                TargetLocation = (!dto.TargetLocation.HasValue)? new Nullable<Model.Vector2Di>() : ToModel(dto.TargetLocation.Value),
                TargetTeamId = dto.TargetTeamId,
                DamageAmount = dto.DamageAmount,
            };
        }

        public static Dto.V1.BattleEvent ToDto(Model.BattleEvent model)
        {
            return new Dto.V1.BattleEvent()
            {
                Time = model.Time,
                Type = ToDto(model.Type),
                SourceEntity = model.SourceEntity,
                SourceLocation = (!model.SourceLocation.HasValue)? new Nullable<Dto.V1.Vector2Di>() : ToDto(model.SourceLocation.Value),
                SourceTeamId = model.SourceTeamId,
                SourceClass = model.SourceClass,
                TargetEntity = model.TargetEntity,
                TargetLocation = (!model.TargetLocation.HasValue)? new Nullable<Dto.V1.Vector2Di>() : ToDto(model.TargetLocation.Value),
                TargetTeamId = model.TargetTeamId,
                DamageAmount = model.DamageAmount,
            };
        }

        public static Model.AttackPlan ToModel(Dto.V1.AttackPlan dto)
        {
            return new Model.AttackPlan()
            {
                TeamId = dto.TeamId,
                Spawns = dto.Spawns.Select( (item) => ToModel(item) ).ToList(),
            };
        }

        public static Dto.V1.AttackPlan ToDto(Model.AttackPlan model)
        {
            return new Dto.V1.AttackPlan()
            {
                TeamId = model.TeamId,
                Spawns = model.Spawns.Select( (item) => ToDto(item) ).ToList(),
            };
        }

        public static Model.DefensePlan ToModel(Dto.V1.DefensePlan dto)
        {
            return new Model.DefensePlan()
            {
                TeamId = dto.TeamId,
                Placements = dto.Placements.Select( (item) => ToModel(item) ).ToList(),
            };
        }

        public static Dto.V1.DefensePlan ToDto(Model.DefensePlan model)
        {
            return new Dto.V1.DefensePlan()
            {
                TeamId = model.TeamId,
                Placements = model.Placements.Select( (item) => ToDto(item) ).ToList(),
            };
        }

        public static Model.DefenderChallenge ToModel(Dto.V1.DefenderChallenge dto)
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

        public static Dto.V1.DefenderChallenge ToDto(Model.DefenderChallenge model)
        {
            return new Dto.V1.DefenderChallenge()
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

        public static Model.Terrain ToModel(Dto.V1.Terrain dto)
        {
            var spawns = new Dictionary<int,IList<Model.Vector2Di>>();
            if (dto.SpawnPointsMap != null)
                foreach (var teamId in dto.SpawnPointsMap.Keys)
                    spawns[teamId] = dto.SpawnPointsMap[teamId].Select( (item) => ToModel(item) ).ToList();
            var goals = new Dictionary<int,IList<Model.Vector2Di>>();
            if (dto.GoalPointsMap != null)
                foreach (var teamId in dto.GoalPointsMap.Keys)
                    goals[teamId] = dto.GoalPointsMap[teamId].Select( (item) => ToModel(item) ).ToList();

            return new Model.Terrain()
            {
                TileTypes = dto.TileTypes.Select( (item) => ToModel(item) ).ToList(),
                Width = dto.Width,
                Height = dto.Height,
                Tiles = dto.Tiles,
                SpawnPointsMap = spawns,
                GoalPointsMap = goals,
            };
        }

        public static Dto.V1.Terrain ToDto(Model.Terrain model)
        {
            var spawns = new Dictionary<int,IList<Dto.V1.Vector2Di>>();
            if (model.SpawnPointsMap != null)
                foreach (var teamId in model.SpawnPointsMap.Keys)
                    spawns[teamId] = model.SpawnPointsMap[teamId].Select( (item) => ToDto(item) ).ToList();
            var goals = new Dictionary<int,IList<Dto.V1.Vector2Di>>();
            if (model.GoalPointsMap != null)
                foreach (var teamId in model.GoalPointsMap.Keys)
                    goals[teamId] = model.GoalPointsMap[teamId].Select( (item) => ToDto(item) ).ToList();

            return new Dto.V1.Terrain()
            {
                TileTypes = model.TileTypes.Select( (item) => ToDto(item) ).ToList(),
                Width = model.Width,
                Height = model.Height,
                Tiles = model.Tiles,
                SpawnPointsMap = spawns,
                GoalPointsMap = goals,
            };
        }

        public static Model.Scenario ToModel(Dto.V1.Scenario dto)
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

        public static Dto.V1.Scenario ToDto(Model.Scenario model)
        {
            return new Dto.V1.Scenario()
            {
                BannerText = model.BannerText.ToList(),
                Terrain = ToDto(model.Terrain),
                AttackPlans = model.AttackPlans.Select( (item) => ToDto(item) ).ToList(),
                DefensePlans = model.DefensePlans.Select( (item) => ToDto(item) ).ToList(),
                Challenges = model.Challenges.Select( (item) => ToDto(item) ).ToList()
            };
        }

        public static Model.BattleResolution ToModel(Dto.V1.BattleResolution dto)
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

        public static Dto.V1.BattleResolution ToDto(Model.BattleResolution model)
        {
            return new Dto.V1.BattleResolution()
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
