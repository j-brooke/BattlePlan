﻿using System;
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

            string filename = null;
            if (args.Length>1)
                filename = args[1];

            if (verb.Equals("play", StringComparison.CurrentCultureIgnoreCase))
                Play(filename);
            else if (verb.Equals("edit", StringComparison.CurrentCultureIgnoreCase))
                Edit(filename);
            else
                Help();
        }

        private static void Play(string filename)
        {
            if (String.IsNullOrWhiteSpace(filename))
            {
                Console.WriteLine("Please provide a filename");
                return;
            }

            var fileContentsAsString = File.ReadAllText(filename);
            var scenario = JsonConvert.DeserializeObject<Scenario>(fileContentsAsString);

            if (scenario.UnitTypes == null)
                scenario.UnitTypes = LoadUnits("scenarios/units.json");

            var resolver = new BattleState();
            var result = resolver.Resolve(scenario);

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
