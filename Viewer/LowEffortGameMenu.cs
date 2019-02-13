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

        private static string[] _bannerText =
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

        private string _scenarioPath;
        private string _sectionPath;
        private List<UnitCharacteristics> _unitsList;

        private void PrintBanner()
        {
            foreach (var line in _bannerText)
                Console.WriteLine(line);
        }

        private void ChooseSection()
        {
            var dirsInScenarios = Directory.EnumerateDirectories(this.ScenariosFolder).ToList();

            Console.Clear();
            PrintBanner();

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
            var filesInSection = Directory.EnumerateFiles(sectionPath, "*.json");

            Console.Clear();
            PrintBanner();

            Console.WriteLine($"Scenarios in {sectionPath}");
            Console.WriteLine();

            foreach (var file in filesInSection)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);

                // TODO: add high score tracking

                Console.WriteLine(name);
            }

            Console.WriteLine();
            Console.Write("Choose a scenario or enter to go back: ");

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

                var viewer = new Viewer.LowEffortViewer() { UseColor = editor.UseColor };
                viewer.ShowBattleResolution(result);

                scenarioToPlay = editor.EditScenario(scenarioToPlay);
            }
        }
    }
}
