using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using BattlePlan;
using Newtonsoft.Json;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Class responsible for loading and saving various items to/from files.  Where appropriate,
    /// it also converts between the DTO stored on disk and the domain objects.
    /// </summary>
    internal class FileLoader
    {
        public bool WritePrettyJson { get; set; } = true;

        public Model.UnitTypeMap LoadUnits()
        {
            var fileContentsAsString = File.ReadAllText(_unitsFileName);
            var unitList = JsonConvert.DeserializeObject<List<Dto.V2.UnitCharacteristics>>(fileContentsAsString);
            var domainUnitList = unitList.Select( (item) => Translator.V2Translator.ToModel(item) ).ToList();
            return new Model.UnitTypeMap(domainUnitList);
        }

        public Model.Scenario LoadScenario(string filename)
        {
            var scenarioFileContents = File.ReadAllText(filename);
            var scenario = JsonConvert.DeserializeObject<Dto.V2.Scenario>(scenarioFileContents);
            var domainScenario = Translator.V2Translator.ToModel(scenario);
            return domainScenario;
        }

        public void SaveScenario(string filename, Model.Scenario scenario)
        {
            var dtoScenario = Translator.V2Translator.ToDto(scenario);
            var fileContentsAsString = JsonConvert.SerializeObject(dtoScenario, GetJsonOpts());
            File.WriteAllText(filename, fileContentsAsString);
        }

        public Generators.GeneratorOptions LoadEditorOptions(string filename)
        {
            var fileContentsAsString = File.ReadAllText(filename);
            var opts = JsonConvert.DeserializeObject<Generators.GeneratorOptions>(fileContentsAsString);
            return opts;
        }

        public void SaveEditorOptions(string filename, Generators.GeneratorOptions opts)
        {
            var fileContentsAsString = JsonConvert.SerializeObject(opts, GetJsonOpts());
            File.WriteAllText(filename, fileContentsAsString);
        }

        public List<HighScoreEntry> LoadHighScores(string filename)
        {
            var fileContentsAsString = File.ReadAllText(filename);
            var highScores = JsonConvert.DeserializeObject<List<HighScoreEntry>>(fileContentsAsString);
            return highScores;
        }

        public void SaveHighScores(string filename, IList<HighScoreEntry> scores)
        {
            var fileContentsAsString = JsonConvert.SerializeObject(scores);
            File.WriteAllText(filename, fileContentsAsString);
        }

        public Model.BattleResolution LoadBattleResolution(string filename)
        {
            var fileContents = File.ReadAllText(filename);
            var resolutionDto = JsonConvert.DeserializeObject<Dto.V2.BattleResolution>(fileContents);
            var resolutionModel = Translator.V2Translator.ToModel(resolutionDto);
            return resolutionModel;

        }

        public void SaveBattleResolution(string filename, Model.BattleResolution resolution)
        {
            var dtoResolution = Translator.V2Translator.ToDto(resolution);
            var fileContentsAsString = JsonConvert.SerializeObject(dtoResolution, GetJsonOpts());
            File.WriteAllText(filename, fileContentsAsString);
        }

        private const string _unitsFileName = "resources/units.json";

        private JsonSerializerSettings GetJsonOpts()
        {
            var opts = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter>(),
            };

            if (this.WritePrettyJson)
            {
                opts.Formatting = Formatting.Indented;
                opts.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            }

            return opts;
        }
    }
}
