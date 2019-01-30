using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using BattlePlan.Common;
using BattlePlan.Resolver;
using BattlePlan.MapGeneration;

namespace BattlePlan
{
    class Program
    {
        static void Main(string[] args)
        {
            NLog.LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration("resources/NLog.config");

            string verb = "play";
            if (args.Length>0)
                verb = args[0];

            string filename = null;
            if (args.Length>1)
                filename = args[1];

            _logger.Info("===Beginning {0} {1}", verb, filename);

            if (verb.Equals("play", StringComparison.CurrentCultureIgnoreCase))
                Play(filename);
            else if (verb.Equals("edit", StringComparison.CurrentCultureIgnoreCase))
                Edit(filename);
            else
                Help();

            NLog.LogManager.Shutdown();
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private const string _unitsFileName = "resources/units.json";

        private static void Play(string filename)
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

            var viewer = new Viewer.LowEffortViewer();
            viewer.ShowBattleResolution(result);
        }

        private static void Edit(string filename)
        {
            var viewer = new Viewer.LowEffortEditor();
            viewer.EditScenario(filename);
        }

        private static void Help()
        {
            Console.WriteLine("Usage: one of");
            Console.WriteLine("  dotnet run play filename");
            Console.WriteLine("  dotnet run edit [filename]");
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
