using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BattlePlan.Model;
using BattlePlan.Resolver;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Console/text menu that lets users choose scenarios to play and keeps track of best scores.
    /// </summary>
    public class LowEffortGameMenu
    {
        public bool UseColor { get; set; }

        public string ScenariosFolder { get; set; } = "scenarios";
        public string GamesFolder { get; set; } = "games";

        public LowEffortGameMenu()
        {
            var unitsFileContents = File.ReadAllText(_unitsFileName);
            _loader = new FileLoader();
            _unitsList = _loader.LoadUnits();
        }

        public void Run()
        {
            LoadHighScores();

            while (true)
            {
                Console.TreatControlCAsInput = false;

                // Choose a section (directory).  If null, exit.
                if (_sectionPath == null)
                    ChooseSection();
                if (_sectionPath == null)
                    return;

                // Choose a scenario file.  If null, back up so we choose a section next iteration.
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

        // In the "sections" list, we want these directory names to show up at the front, if present.
        private static readonly string[] _fixedSectionOrder = { "how-to-play", "beginner", "advanced" };

        private string _scenarioPath;
        private string _sectionPath;
        private UnitTypeMap _unitsList;
        private List<HighScoreEntry> _highScores;
        private FileLoader _loader;

        private void PrintBanner(bool large)
        {
            var strings = (large)? _bigBannerText : _smallBannerText;
            foreach (var line in strings)
                Console.WriteLine(line);
        }

        private void ChooseSection()
        {
            var sectionNames = Directory.EnumerateDirectories(this.ScenariosFolder)
                .Select( (path) => System.IO.Path.GetRelativePath(this.ScenariosFolder, path) );
            var sortedNames = SortSemiFixedOrder(sectionNames, _fixedSectionOrder)
                .ToList();

            Console.Clear();
            PrintBanner(true);

            for (int i=0; i<sortedNames.Count; ++i)
                Console.WriteLine($"{i+1} {sortedNames[i]}");

            Console.WriteLine();
            Console.Write($"Choose a section (1-{sortedNames.Count} or enter to quit): ");
            var input = Console.ReadLine();

            int chosenNumber = 0;
            int.TryParse(input, out chosenNumber);
            if (chosenNumber>=1 && chosenNumber<=sortedNames.Count)
                _sectionPath = System.IO.Path.Join(this.ScenariosFolder, sortedNames[chosenNumber-1]);
            else
                _sectionPath = null;
        }

        private void ChooseScenario(string sectionPath)
        {
            // We assume any .json files here are scenarios.
            var filesInSection = Directory.EnumerateFiles(sectionPath, "*.json").ToList();
            filesInSection.Sort(FileSortComparison);

            Console.Clear();
            PrintBanner(false);

            // Show a table of the scenario name, and their best (lowest) resource score for each star challenge
            // level, and the date it was achieved.
            var tableFormat = "{0,-12}{1,-18}{2,-18}{3,-18}";
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
            // Make a copy of the scenario in the games folder, if it doesn't already exist.  That lets the
            // user save their scenario with their defender placements, if they want, without affecting the
            // original scenario file.
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
                    _highScores = _loader.LoadHighScores(_highScoreFileName);
                }
                catch (IOException ex)
                {
                    _logger.Error("Can't load high score file", ex);
                }
                catch (Newtonsoft.Json.JsonException ex)
                {
                    _logger.Error("Can't load high score file", ex);
                }
            }
        }

        private void SaveHighScores()
        {
            try
            {
                _loader.SaveHighScores(_highScoreFileName, _highScores);
            }
            catch (IOException ex)
            {
                _logger.Error("Can't save high score file", ex);
            }
            catch (Newtonsoft.Json.JsonException ex)
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

        /// <summary>
        /// Comparison to sort file names.  The strings are compared as numbers if they convert to
        /// integers; otherwise they're compared as strings as normal.
        /// </summary>
        private int FileSortComparison(string pathA, string pathB)
        {
            var nameA = System.IO.Path.GetFileNameWithoutExtension(pathA).ToLower();
            var nameB = System.IO.Path.GetFileNameWithoutExtension(pathB).ToLower();

            if (nameA == nameB)
                return 0;

            int numberA = -1;
            int.TryParse(nameA, out numberA);

            int numberB = -1;
            int.TryParse(nameB, out numberB);

            if (numberA >= 0)
            {
                if (numberB >= 0)
                    return numberA - numberB;
                else
                    return -1;
            }
            else
            {
                if (numberB >= 0)
                    return +1;
                else
                    return string.Compare(nameA, nameB);
            }
        }

        /// <summary>
        /// Orders a list, prioritizing items in the given fixedOrderList.  Items present in fixedOrderList
        /// will be returned in the same order as that sequence.  Items that aren't present in fixedOrderList
        /// will be after the ones that are, and sorted according to T's default comparison.
        /// </summary>
        private IEnumerable<T> SortSemiFixedOrder<T>(IEnumerable<T> sequence, IEnumerable<T> fixedOrderList)
            where T : IComparable<T>, IEquatable<T>
        {
            var fixedPart = fixedOrderList.Intersect(sequence);
            var extrasPart = sequence.Except(fixedOrderList)
                .OrderBy( (item) => item );
            return fixedPart.Concat(extrasPart);
        }
    }
}
