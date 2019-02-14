using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BattlePlan.Common;
using BattlePlan.Resolver;
using Newtonsoft.Json;

namespace BattlePlan.Viewer
{
    public class LowEffortGameMenu
    {
        public bool UseColor { get; set; }

        public string ScenariosFolder { get; set; } = "scenarios";
        public string GamesFolder { get; set; } = "games";

        public LowEffortGameMenu()
        {
            var unitsFileContents = File.ReadAllText(_unitsFileName);
            _unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(unitsFileContents);
        }

        public void Run()
        {
            LoadHighScores();

            while (true)
            {
                Console.TreatControlCAsInput = false;

                if (_sectionPath == null)
                    ChooseSection();
                if (_sectionPath == null)
                    return;

                ChooseScenario(_sectionPath);
                if (_scenarioPath != null)
                    LaunchScenario(_scenarioPath);
                else
                    _sectionPath = null;
            }
        }

        private const string _unitsFileName = "resources/units.json";
        private const string _highScoreFileName = "games/highscores.json";

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private static string[] _bigBannerText =
        {
            "*****      *    ******* ******* **      ******* *****   **         *    **    *",
            "**   *    ***     **      **    **      **      **   ** **        ***   ***   *",
            "**   *   **  *    **      **    **      **      **   ** **       **  *  ** *  *",
            "*****   **    *   **      **    **      ******* *****   **      **    * **  * *",
            "**   ** *******   **      **    **      **      **      **      ******* **   **",
            "**   ** **    *   **      **    **      **      **      **      **    * **   **",
            "******  **    *   **      **    ******* ******* **      ******* **    * **    *",
            "",
            "A game of epic ASCII tactics",
            "",
        };

        private static string[] _smallBannerText =
        {
            "BattlePlan",
            "A game of epic ASCII tactics",
            "",
        };

        private string _scenarioPath;
        private string _sectionPath;
        private List<UnitCharacteristics> _unitsList;
        private List<HighScoreEntry> _highScores;

        private void PrintBanner(bool large)
        {
            var strings = (large)? _bigBannerText : _smallBannerText;
            foreach (var line in strings)
                Console.WriteLine(line);
        }

        private void ChooseSection()
        {
            var dirsInScenarios = Directory.EnumerateDirectories(this.ScenariosFolder).ToList();

            Console.Clear();
            PrintBanner(true);

            for (int i=0; i<dirsInScenarios.Count; ++i)
            {
                var dirName = System.IO.Path.GetRelativePath(this.ScenariosFolder, dirsInScenarios[i]);
                Console.WriteLine($"{i+1} {dirName}");
            }

            Console.WriteLine();
            Console.Write($"Choose a section (1-{dirsInScenarios.Count} or enter to quit): ");
            var input = Console.ReadLine();

            int chosenNumber = 0;
            int.TryParse(input, out chosenNumber);
            if (chosenNumber>=1 && chosenNumber<=dirsInScenarios.Count)
                _sectionPath = dirsInScenarios[chosenNumber-1];
            else
                _sectionPath = null;
        }

        private void ChooseScenario(string sectionPath)
        {
            // We assume any .json files here are scenarios.
            var filesInSection = Directory.EnumerateFiles(sectionPath, "*.json");

            Console.Clear();
            PrintBanner(false);

            // Show a table of the scenario name, and their best (lowest) resource score for each star challenge
            // level, and the date it was achieved.
            var tableFormat = "{0,-12}{1,-15}{2,-15}{3,-15}";
            Console.WriteLine(string.Format(tableFormat, "Scenario", "1-star", "2-star", "3-star"));

            foreach (var file in filesInSection)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);

                var oneStar = GetHighScore(file, "*");
                var twoStar = GetHighScore(file, "**");
                var threeStar = GetHighScore(file, "***");

                var line = string.Format(tableFormat, name, HighScoreString(oneStar), HighScoreString(twoStar), HighScoreString(threeStar));
                Console.WriteLine(line);
            }

            Console.WriteLine();
            Console.Write("Choose a scenario or enter to go back: ");

            // We're assuming they're giving us the actual file name (minus the .json extension).  The reason is
            // that the file names will probably just be numbers anyway.
            var input = Console.ReadLine();

            _scenarioPath = null;
            foreach (var file in filesInSection)
            {
                var simpleName = System.IO.Path.GetFileNameWithoutExtension(file).ToLower();
                if (simpleName==input?.ToLower())
                {
                    _scenarioPath = file;
                    return;
                }
            }
        }

        /// <summary>
        /// Runs the editor in player-view on the selected scenario.  If the user hits view-resolution,
        /// the editor will return a scenario that we pass along to the resolver and viewer.
        /// </summary>
        private void LaunchScenario(string scenarioPath)
        {
            // Copy the subdirectories and file name from the scenario directory on, and then
            // use those to create a parallel path in the games directory.
            var partialPath = System.IO.Path.GetRelativePath(this.ScenariosFolder, scenarioPath);
            var gamePath = System.IO.Path.Combine(this.GamesFolder, partialPath);

            if (!File.Exists(gamePath))
            {
                // Make sure the directory exists, and then copy the scenario into the appropriate
                // spot in the games directory.
                var finalGameDir = Directory.GetParent(gamePath);
                Directory.CreateDirectory(finalGameDir.FullName);
                File.Copy(scenarioPath, gamePath);
            }

            var editor = new Viewer.LowEffortEditor()
            {
                UseColor = this.UseColor,
                PlayerView = true,
            };

            // Loop between the editor/resolver/viewer until the user asks to quit,
            // signified by a null return from the editor.
            Scenario scenarioToPlay = editor.EditScenario(gamePath);
            while (scenarioToPlay != null)
            {
                // TODO: Should we automatically save the game in the current state?

                Console.WriteLine("Please wait - resolving battle");

                var resolver = new BattleState();
                var result = resolver.Resolve(scenarioToPlay, _unitsList);

                UpdateHighScoresIfNecessary(scenarioPath, result);

                var viewer = new Viewer.LowEffortViewer() { UseColor = editor.UseColor };
                viewer.ShowBattleResolution(result);

                scenarioToPlay = editor.EditScenario(scenarioToPlay);
            }
        }

        private void LoadHighScores()
        {
            _highScores = new List<HighScoreEntry>();

            if (File.Exists(_highScoreFileName))
            {
                try
                {
                    var fileContentsAsString = File.ReadAllText(_highScoreFileName);
                    _highScores = JsonConvert.DeserializeObject<List<HighScoreEntry>>(fileContentsAsString);
                }
                catch (IOException ex)
                {
                    _logger.Error("Can't load high score file", ex);
                }
                catch (JsonException ex)
                {
                    _logger.Error("Can't load high score file", ex);
                }
            }
        }

        private void SaveHighScores()
        {
            try
            {
                var fileContentsAsString = JsonConvert.SerializeObject(_highScores);
                File.WriteAllText(_highScoreFileName, fileContentsAsString);
            }
            catch (IOException ex)
            {
                _logger.Error("Can't save high score file", ex);
            }
            catch (JsonException ex)
            {
                _logger.Error("Can't save high score file", ex);
            }
        }

        private HighScoreEntry GetHighScore(string scenarioPath, string challengeName)
        {
            return _highScores.FirstOrDefault( (hse) => hse.ScenarioPath==scenarioPath && hse.ChallengeName==challengeName );
        }

        private bool UpdateHighScore(string scenarioPath, string challengeName, int resourceCost)
        {
            var existingEntry = GetHighScore(scenarioPath, challengeName);
            if (existingEntry == null)
            {
                var newEntry = new HighScoreEntry()
                {
                    ScenarioPath = scenarioPath,
                    ChallengeName = challengeName,
                    BestResourceCost = resourceCost,
                    BestDate = DateTime.Now,
                };
                _highScores.Add(newEntry);
                return true;
            }
            else if (resourceCost < existingEntry.BestResourceCost)
            {
                existingEntry.BestResourceCost = resourceCost;
                existingEntry.BestDate = DateTime.Now;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void UpdateHighScoresIfNecessary(string scenarioPath, BattleResolution resolution)
        {
            if (resolution?.ChallengesAchieved != null)
            {
                bool wereAnyUpdated = false;
                foreach (var chal in resolution.ChallengesAchieved)
                {
                    int resourcesUsed = 0;
                    resolution.DefenderResourceTotals.TryGetValue(chal.PlayerTeamId, out resourcesUsed);
                    wereAnyUpdated = UpdateHighScore(scenarioPath, chal.Name, resourcesUsed);
                }

                if (wereAnyUpdated)
                    SaveHighScores();
            }
        }

        private string HighScoreString(HighScoreEntry entry)
        {
            if (entry == null)
                return "---";
            else
                return $"{entry.BestDate:d} ({entry.BestResourceCost})";
        }
    }
}
