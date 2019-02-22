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
    public class FileLoader
    {
        public bool WritePrettyJson { get; set; } = true;

        public List<Model.UnitCharacteristics> LoadUnits()
        {
            var fileContentsAsString = File.ReadAllText(_unitsFileName);
            var unitList = JsonConvert.DeserializeObject<List<Dto.V1.UnitCharacteristics>>(fileContentsAsString);
            var domainUnitList = unitList.Select( (item) => Translator.V1Translator.ToModel(item) ).ToList();
            return domainUnitList;
        }

        public Model.Scenario LoadScenario(string filename)
        {
            var scenarioFileContents = File.ReadAllText(filename);
            var scenario = JsonConvert.DeserializeObject<Dto.V1.Scenario>(scenarioFileContents);
            var domainScenario = Translator.V1Translator.ToModel(scenario);
            return domainScenario;
        }

        public void SaveScenario(string filename, Model.Scenario scenario)
        {
            var dtoScenario = Translator.V1Translator.ToDto(scenario);
            var fileContentsAsString = JsonConvert.SerializeObject(dtoScenario, GetJsonOpts());
            File.WriteAllText(filename, fileContentsAsString);
        }

        public MapGeneration.GeneratorOptions LoadGeneratorOptions(string filename)
        {
            var fileContentsAsString = File.ReadAllText(filename);
            var mapGenOpts = JsonConvert.DeserializeObject<MapGeneration.GeneratorOptions>(fileContentsAsString);
            return mapGenOpts;
        }

        public void SaveGeneratorOptions(string filename, MapGeneration.GeneratorOptions opts)
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

        private const string _unitsFileName = "resources/units.json";

        private JsonSerializerSettings GetJsonOpts()
        {
            var opts = new JsonSerializerSettings()
            {
                Formatting = (this.WritePrettyJson)?  Formatting.Indented : Formatting.None,
            };

            return opts;
        }
    }
}
