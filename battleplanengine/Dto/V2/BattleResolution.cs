using System;
using System.Collections.Generic;

namespace BattlePlanEngine.Dto.V2
{
    public class BattleResolution
    {
        /// <summary>
        /// Optional text shown at the top of the screen for tutorials or flavor.
        /// </summary>
        public IList<string> BannerText { get; set; }
        public Terrain Terrain { get; set; }
        public IList<UnitCharacteristics> UnitTypes { get; set; }
        public IList<BattleEvent> Events { get; set; }

        /// <summary>
        /// Key value pairs where the key is the TeamID of an attacking team and the value
        /// is the number of attackers who reached their goal.
        /// </summary>
        public IDictionary<int,int> AttackerBreachCounts { get; set; }

        /// <summary>
        /// Key value pairs where the key is the TeamID of a defending team and the value
        /// is the number of defenders who died.
        /// </summary>
        public IDictionary<int,int> DefenderCasualtyCounts { get; set; }

        public IList<DefenderChallenge> ChallengesAchieved { get; set; }
        public IList<DefenderChallenge> ChallengesFailed { get; set; }

        /// <summary>
        /// Key value pairs where the key is TeamID and the value is the resource total of attacking
        /// units.
        /// </summary>
        public IDictionary<int,int> AttackerResourceTotals { get; set; }

        /// <summary>
        /// Key value pairs where the key is TeamID and the value is the resource total of defending
        /// units.
        /// </summary>
        public IDictionary<int,int> DefenderResourceTotals { get; set; }

        public IList<string> ErrorMessages { get; set; }
    }
}
