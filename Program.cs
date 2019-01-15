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
            string verb = "play";
            if (args.Length>0)
                verb = args[0];

            if (verb.Equals("play", StringComparison.CurrentCultureIgnoreCase))
                Play();
            else if (verb.Equals("map", StringComparison.CurrentCultureIgnoreCase))
                MapGen();
        }

        private static void Play()
        {
            var fileContentsAsString = File.ReadAllText("scenarios/test1.json");
            var scenario = JsonConvert.DeserializeObject<Scenario>(fileContentsAsString);

            if (scenario.UnitTypes == null)
                scenario.UnitTypes = LoadUnits("scenarios/units.json");

            var resolver = new BattleState();
            var result = resolver.Resolve(scenario);

            foreach (var evt in result.Events)
            {
                Console.WriteLine(evt.ToString());
            }

            var viewer = new Viewer.LowEffortViewer();
            viewer.Show(result);
        }

        private static void MapGen()
        {
            var opts = new GeneratorOptions()
            {
                Height = 36,
                Width = 50,
                ChunkSizeX = 7,
                ChunkSizeY = 4,
                PositiveCycleCount = 60,
                NegativeCycleCount = 10,
                SpawnPointCount = 3,
                GoalCount = 3,
            };

            var generator = new Generator(opts);
            var terrain = generator.Create();

            var result = new BattleResolution()
            {
                Terrain = terrain,
                Events = new List<BattleEvent>(),
            };

            var outputAsString = JsonConvert.SerializeObject(terrain, _serialOpts);
            File.WriteAllText("scenarios/map1.json", outputAsString);

            var viewer = new Viewer.LowEffortViewer();
            viewer.Show(result);
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
