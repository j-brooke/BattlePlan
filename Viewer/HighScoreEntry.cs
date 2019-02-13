using System;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Structure used to keep track of how well a player did on a scenario and challenge.
    /// </summary>
    public class HighScoreEntry
    {
        public string ScenarioPath { get; set; }
        public string ChallengeName { get; set; }
        public int BestResourceCost { get; set; }
        public DateTime BestDate { get; set; }
    }
}
