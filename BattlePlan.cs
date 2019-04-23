using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattlePlan;
using BattlePlan.Resolver;
using Mono.Options;

namespace BattlePlan
{
    static class BattlePlan
    {
        static void Main(string[] args)
        {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration("resources/NLog.config");

            try
            {
                _logger.Info("===Beginning BattlePlan: " + string.Join(' ', args));

                bool showHelp = false;
                _cmdOptions = new OptionSet()
                {
                    {
                        "h|help",
                        "show this help info and exit",
                        v => showHelp = (v!=null)
                    },
                    {
                        "m|monochrome",
                        "don't use color",
                        v => _monochrome = (v!=null)
                    }
                };

                IList<string> leftoverArgs;
                try
                {
                    leftoverArgs = _cmdOptions.Parse(args);
                }
                catch (OptionException e)
                {
                    Console.WriteLine(_appName + ": " + e.Message);
                    Console.WriteLine($"Try '{_appName} --help' for more information.");
                    return;
                }

                if (showHelp)
                {
                    Help();
                    return;
                }

                string verb = "menu";
                if (leftoverArgs.Count>0)
                    verb = leftoverArgs[0].ToLower();

                string filename = null;
                if (leftoverArgs.Count>1)
                    filename = leftoverArgs[1];

                switch (verb)
                {
                    case "resolve":
                        ResolveAndShow(filename);
                        break;
                    case "edit":
                        Edit(filename, false);
                        break;
                    case "play":
                        Edit(filename, true);
                        break;
                    case "menu":
                        GameMenu();
                        break;
                    default:
                        Help();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex);
            }

            NLog.LogManager.Shutdown();
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private const string _appName = "BattlePlan.dll";

        private static OptionSet _cmdOptions;
        private static bool _monochrome = false;
        private static Viewer.FileLoader _loader = new Viewer.FileLoader();

        /// <summary>
        /// "Resolves" a scenario file, figuring out which teams do what and who wins, and then
        /// displays the result as animated ASCII characters.
        /// </summary>
        private static void ResolveAndShow(string filename)
        {
            if (String.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Please provide a filename");
                return;
            }

            var scenario = _loader.LoadScenario(filename);
            var unitsList = _loader.LoadUnits();

            var resolver = new BattleState();
            var result = resolver.Resolve(scenario, unitsList);

            var viewer = new Viewer.LowEffortViewer() { UseColor = !_monochrome };
            viewer.ShowBattleResolution(result);
        }

        /// <summary>
        /// Launches the editor, to allow changing scenarios.  If playerView is true, the user
        /// is restricted to only changing Team 2's defense plan.  Otherwise, the user can manipulate
        /// the terrain, spawns/goals, challenges, and all teams' attack and defense plans.
        /// </summary>
        private static void Edit(string filename, bool playerView)
        {
            // Only allow playerView if a filename is provided.  Not much fun to play an empty field
            // with no spawns, goals, or attackers.
            if (playerView && String.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Please provide a filename");
                return;
            }

            Model.UnitTypeMap unitsList = null;
            var editor = new Viewer.LowEffortEditor()
            {
                UseColor = !_monochrome,
                PlayerView = playerView,
            };

            // If EditScenario returns a scenario object, the user wants to see the resolution.
            // Keep looping between edit and resolve until edit returns null, meaning exit.
            Model.Scenario scenarioToPlay = editor.EditScenario(filename);

            while (scenarioToPlay != null)
            {
                Console.WriteLine("Please wait - resolving battle");

                if (unitsList==null)
                    unitsList = _loader.LoadUnits();

                var resolver = new BattleState();
                var result = resolver.Resolve(scenarioToPlay, unitsList);

                var viewer = new Viewer.LowEffortViewer() { UseColor = editor.UseColor };
                viewer.ShowBattleResolution(result);

                scenarioToPlay = editor.EditScenario(scenarioToPlay);
            }
        }

        private static void Help()
        {
            Console.WriteLine("Usage: one of");
            Console.WriteLine($"  dotnet {_appName} menu");
            Console.WriteLine($"  dotnet {_appName} resolve <filename>");
            Console.WriteLine($"  dotnet {_appName} edit");
            Console.WriteLine($"  dotnet {_appName} play <filename>");

            _cmdOptions.WriteOptionDescriptions(Console.Out);
        }

        private static void GameMenu()
        {
            var menu = new Viewer.LowEffortGameMenu() { UseColor = !_monochrome };
            menu.Run();
        }
    }
}
