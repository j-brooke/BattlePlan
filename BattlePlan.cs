using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using BattlePlan.Common;
using BattlePlan.Resolver;
using BattlePlan.MapGeneration;
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

                string verb = "play";
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

        private const string _appName = "BattlePlan";

        private const string _unitsFileName = "resources/units.json";

        private static OptionSet _cmdOptions;
        private static bool _monochrome = false;

        private static void ResolveAndShow(string filename)
        {
            if (String.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Please provide a filename");
                return;
            }

            var scenarioFileContents = File.ReadAllText(filename);
            var scenario = JsonConvert.DeserializeObject<Scenario>(scenarioFileContents);

            var unitsFileContents = File.ReadAllText(_unitsFileName);
            var unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(unitsFileContents);

            var resolver = new BattleState();
            var result = resolver.Resolve(scenario, unitsList);

            var viewer = new Viewer.LowEffortViewer() { UseColor = !_monochrome };
            viewer.ShowBattleResolution(result);
        }

        private static void Edit(string filename, bool playerView)
        {
            // Only allow playerView if a filename is provided.  Not much fun to play an empty field
            // with no spawns, goals, or attackers.
            if (playerView && String.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Please provide a filename");
                return;
            }

            List<UnitCharacteristics> unitsList = null;
            var editor = new Viewer.LowEffortEditor()
            {
                UseColor = !_monochrome,
                PlayerView = playerView,
            };

            Scenario scenarioToPlay = editor.EditScenario(filename);

            while (scenarioToPlay != null)
            {
                Console.WriteLine("Please wait - resolving battle");

                if (unitsList==null)
                {
                    var unitsFileContents = File.ReadAllText(_unitsFileName);
                    unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(unitsFileContents);
                }

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
            Console.WriteLine("  dotnet run play filename");
            Console.WriteLine("  dotnet run edit [filename]");

            _cmdOptions.WriteOptionDescriptions(Console.Out);
        }

        private static JsonSerializerSettings _serialOpts = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };
        private static JsonSerializerSettings _serialOptsNoIndent = new JsonSerializerSettings()
        {
            Formatting = Formatting.None,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        private static List<UnitCharacteristics> LoadUnits(string filename)
        {
            var fileContentsAsString = File.ReadAllText(filename);
            var unitList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(fileContentsAsString);
            return unitList;
        }
    }
}
