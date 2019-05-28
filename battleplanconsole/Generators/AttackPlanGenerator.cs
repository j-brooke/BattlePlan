using System;
using System.Collections.Generic;
using System.Linq;
using BattlePlanEngine.Model;


namespace BattlePlanConsole.Generators
{
    /// <summary>
    /// Class for generating random attack plans.
    /// </summary>
    public class AttackPlanGenerator
    {
        public AttackPlanGenerator(AttackPlanGeneratorOptions options, UnitTypeMap unitTypes)
        {
            _options = options;
            _unitTypes = unitTypes;

            // TODO: Maybe include an option to pass a RNG seed, so that we can easily reproduce results.
            _rng = new Random();

            _allFodderUnits = options.FodderUnitNames?.Select( (name) => _unitTypes.Get(name) ).ToList();
            _allEliteUnits = options.EliteUnitNames?.Select( (name) => _unitTypes.Get(name) ).ToList();
            _allLonerUnits = options.LonerUnitNames?.Select( (name) => _unitTypes.Get(name) ).ToList();
        }

        public AttackPlan Create(int teamId, int numberOfSpawnPoints)
        {
            // Not all types of units will show up in a given attack plan.  Choose which are
            // possible for the one we're creating.
            PickActiveFodderUnits();
            PickActiveEliteUnits();
            PickActiveLonerUnits();

            // Create groups of spawns without worrying about the where or when.
            var spawnGroups = new List<IList<UnitCharacteristics>>();

            // Create a bunch of spawn groups.  Each will have one type of fodder and/or one type of elite.
            var minGroupCost = _options.CostTiers.Min();
            var remainingBudget = _options.TotalResourceBudget;
            while (remainingBudget >= minGroupCost)
            {
                var tier = _rng.Next(_options.CostTiers.Count);
                if (remainingBudget >= _options.CostTiers[tier])
                {
                    var group = CreateSpawnGroup(tier);
                    remainingBudget -= group.Aggregate(0, (acc, unit) => acc + unit.ResourceCost );
                    spawnGroups.Add(group);
                }
            }

            // If there are any resource points leftover, create some loner spawn groups (currently just scouts).
            var chosenLoner = ChooseOne(_activeLonerUnits);
            while (remainingBudget >= chosenLoner.ResourceCost)
            {
                var group = new UnitCharacteristics[] { chosenLoner };
                remainingBudget -= chosenLoner.ResourceCost;
                spawnGroups.Add(group);
            }

            // Randomly reorder the groups.  (They're already kinda random, but there's a bias for large groups
            // near the front of the list.)
            Shuffle(spawnGroups);

            // Distribute the spawn groups across different spawn points and times.
            var plan = new AttackPlan() { TeamId = teamId };
            int time = 0;
            int spawnGroupIdx = 0;
            while (spawnGroupIdx < spawnGroups.Count)
            {
                int timeInc = _options.MinimumTimeBetweenSpawnGroups;

                var spawnPts = Enumerable.Range(0, numberOfSpawnPoints).ToArray();
                Shuffle(spawnPts);

                for (int i = 0; i<spawnPts.Length && spawnGroupIdx<spawnGroups.Count; ++i)
                {
                    var group = spawnGroups[spawnGroupIdx];
                    AddSpawnList(plan, group, time, spawnPts[i]);
                    spawnGroupIdx += 1;

                    // Make sure that the next bunch of spawns don't happen too soon.  We're arbitrarily allocating
                    // 1 second per unit and a buffer, but we might want to take unit movement speed into account at some point.
                    timeInc = Math.Max(timeInc, group.Count + _options.InterGroupTime);
                }
                time += timeInc;
            }

            return plan;
        }

        private readonly AttackPlanGeneratorOptions _options;
        private UnitTypeMap _unitTypes;
        private Random _rng;

        private IList<UnitCharacteristics> _allFodderUnits;
        private IList<UnitCharacteristics> _allEliteUnits;
        private IList<UnitCharacteristics> _allLonerUnits;

        private IList<UnitCharacteristics> _activeFodderUnits;
        private IList<UnitCharacteristics> _activeEliteUnits;
        private IList<UnitCharacteristics> _activeLonerUnits;

        /// <summary>
        /// Randomly pick and return 1 element from a list (or default if null or empty).
        /// </summary>
        private T ChooseOne<T>(IList<T> sourceList)
        {
            if (sourceList==null || sourceList.Count==0)
                return default(T);

            int index = _rng.Next(sourceList.Count);
            return sourceList[index];
        }

        /// <summary>
        /// Randomly reorders the elements of the given list.  Modifies the original list, not a copy.
        /// </summary>
        private void Shuffle<T>(IList<T> list)
        {
            // At any given point, the range 0..selectForIdx contains the elements that haven't been picked yet.
            // For each position in the list, we want to pick one of those for that position, swaping out the
            // current value.
            int selectForIdx = list.Count-1;
            while (selectForIdx>0)
            {
                var idxToSwap = _rng.Next(selectForIdx+1);

                var temp = list[idxToSwap];
                list[idxToSwap] = list[selectForIdx];
                list[selectForIdx] = temp;

                selectForIdx -= 1;
            }
        }

        /// <summary>
        /// Randomly pick which fodder units can show up in the generated AttackPlan.
        /// </summary>
        private void PickActiveFodderUnits()
        {
            // 50% chance for each fodder type to be present.  But there must be at least one.
            var list = new List<UnitCharacteristics>();

            if (_allFodderUnits==null || _allFodderUnits.Count==0)
                throw new InvalidOperationException("AttackPlanGeneratorOptions.FodderUnitNames may not be empty");


            while (list.Count==0)
            {
                foreach (var fodderType in _allFodderUnits)
                {
                    if (_rng.Next(2)==0)
                        list.Add(fodderType);
                }
            }

            _activeFodderUnits = list;
        }

        /// <summary>
        /// Randomly pick which elite units can show up in the generated AttackPlan.
        /// </summary>
        private void PickActiveEliteUnits()
        {
            var list = new List<UnitCharacteristics>();

            var numEliteTypes = Math.Min(_allEliteUnits.Count, _options.NumberOfEliteTypesPerPlan);

            while (list.Count < numEliteTypes)
            {
                var choice = ChooseOne(_allEliteUnits);
                if (!list.Contains(choice))
                    list.Add(choice);
            }

            _activeEliteUnits = list;
        }

        /// <summary>
        /// Randomly pick which loner units can show up in the generated AttackPlan.
        /// (Okay, right now it's not all that random since I've only got one loner type.)
        /// </summary>
        private void PickActiveLonerUnits()
        {
            _activeLonerUnits = _allLonerUnits;
        }

        /// <summary>
        /// Create a group of spawns: several units that will spawn at one time and one spawn point.
        /// </summary>
        private IList<UnitCharacteristics> CreateSpawnGroup(int tier)
        {
            int remainingBudget = _options.CostTiers[tier];
            var spawnList = new List<UnitCharacteristics>();

            // If this spawn group has elites at all, it will be either all elites, or half elite and half
            // fodder (by resource cost).
            var hasElites = (_rng.NextDouble()<_options.EliteProbTiers[tier]);
            var allAreElites = hasElites && (_rng.Next(2)==0);

            if (hasElites)
            {
                var chosenElite = ChooseOne(_activeEliteUnits);
                var remainingEliteBudget = (allAreElites)? remainingBudget : remainingBudget / 2;

                while (chosenElite.ResourceCost <= remainingEliteBudget)
                {
                    spawnList.Add(chosenElite);
                    remainingEliteBudget -= chosenElite.ResourceCost;
                    remainingBudget -= chosenElite.ResourceCost;
                }
            }

            if (!allAreElites)
            {
                var chosenFodder = ChooseOne(_activeFodderUnits);

                while (chosenFodder.ResourceCost <= remainingBudget)
                {
                    spawnList.Add(chosenFodder);
                    remainingBudget -= chosenFodder.ResourceCost;
                }

                // Reverse the list.  If it's mixed elites and fodder, let the fodder go first.  (Berserkers won't like
                // it, but crossbowmen and storm mages strongly endorse this policy.)
                spawnList.Reverse();
            }

            return spawnList;
        }

        /// <summary>
        /// Add units to the given AttackPlan's spawn list at the given time and spawn point.
        /// </summary>
        private void AddSpawnList(AttackPlan plan, IList<UnitCharacteristics> units, double time, int spawnPtIdx)
        {
            var spawns = units.Select( (unit) => new AttackerSpawn()
                {
                    Time = time,
                    UnitType = unit.Name,
                    SpawnPointIndex = spawnPtIdx
                });
            plan.Spawns = plan.Spawns.Concat(spawns).ToList();
        }
    }
}
