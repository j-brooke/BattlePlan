using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using BattlePlan.Common;
using BattlePlan.MapGeneration;
using BattlePlan.Resolver;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Interactive terminal-based tool for editing maps and scenarios.
    /// </summary>
    public class LowEffortEditor
    {
        public bool UseColor { get; set;  } = true;

        /// <summary>
        /// If true, the user is locked in to defender playement mode for team 2, and they
        /// get a slightly simplified sidebar.
        /// </summary>
        public bool PlayerView { get; set; } = false;

        public LowEffortEditor()
        {
            LoadOrCreateGeneratorOptions();
            _unitTypes = LoadUnitsFile();
        }

        /// <summary>
        /// Runs the interactive editor on the scenario at the given file.  Returns null if the user wants
        /// to properly quit, or a scenario to be passed to the resolver if they want to play it.
        /// </summary>
        public Scenario EditScenario(string filename)
        {
            if (!String.IsNullOrWhiteSpace(filename))
            {
                var fileContentsAsString = File.ReadAllText(filename);
                var newScenario = JsonConvert.DeserializeObject<Scenario>(fileContentsAsString);

                _lastScenarioFilename = filename;
                return EditScenario(newScenario);
            }
            else
            {
                return EditScenario((Scenario)null);
            }
        }

        /// <summary>
        /// Runs the interactive editor with the given scenario.  Returns null if the user wants
        /// to properly quit, or a scenario to be passed to the resolver if they want to play it.
        /// </summary>
        public Scenario EditScenario(Scenario scenario)
        {
            _scenario = scenario;
            if (_scenario == null)
                _scenario = new Scenario();
            if (_scenario.Terrain == null)
                _scenario.Terrain = Terrain.NewDefault();
            InitFromScenario();

            if (!TestScreenSize(_scenario.Terrain))
                return null;

            _exitEditor = false;
            _playAfterExit = false;
            while (!_exitEditor)
            {
                // If we're supposed to show defenders' range/line-of-site regions, make sure we've
                // got the grid built.  We may have deleted it when we added/removed defenders.
                if (_terrainOverlayTiles==null)
                    BuildTerrainOverlay();

                var terrainOverride = (_showDefenderLOS || _showForbiddenTiles)? _terrainOverlayTiles : null;

                // Draw the map and everything on it.
                _canvas.BeginFrame();
                WriteModeHelp();
                _canvas.PaintTerrain(_scenario.Terrain, terrainOverride, 0, 0);
                _canvas.PaintSpawnPoints(_scenario.Terrain, 0, 0);
                _canvas.PaintGoalPoints(_scenario.Terrain, 0, 0);
                DrawDefenderPlacements();
                WriteStatusMessage();
                _canvas.EndFrame();

                _statusMsg = null;
                _canvas.ShowCursor(_cursorX, _cursorY);
                ProcessUserInput();

                // Set the default status bar if nothing else set
                if (_statusMsg==null)
                    SetDefaultStatusBar();
            }

            _canvas.Shutdown();

            if (_playAfterExit)
                return _scenario;
            else
                return null;
        }
        private const int _minimumTeamId = 1;
        private const int _maximumTeamId = 2;
        private const int _playerViewTeamId = 2;

        private const string _unitsFileName = "resources/units.json";

        private const string _optionsFile = ".options.json";

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        // Write stuff as nicely-formatted JSON.
        // TODO: Make this optional?
        private static JsonSerializerSettings _jsonOpts = new JsonSerializerSettings()
        {
            Formatting = Formatting.Indented,
        };

        private readonly LowEffortCanvas _canvas = new LowEffortCanvas();
        private GeneratorOptions _mapGenOptions;
        private Scenario _scenario;
        private int _cursorX;
        private int _cursorY;
        private bool _exitEditor;
        private bool _playAfterExit;
        private EditorMode _mode;
        private bool _paintEnabled;
        private string _statusMsg;
        private string _lastScenarioFilename;
        private int _teamId;

        private int _spawnTime;
        private int _selectedSpawnPointIndex;
        private List<UnitCharacteristics> _attackerClasses;
        private List<UnitCharacteristics> _defenderClasses;
        private bool _showDefenderLOS;
        private bool _showForbiddenTiles;
        private int[,] _terrainOverlayTiles;
        private int _totalResourceCost;
        private IList<UnitCharacteristics> _unitTypes;


        /// <summary>
        /// Called on start, or when a new scenario is loaded.  Rebuilds internal data and such.
        /// </summary>
        private void InitFromScenario()
        {
            // Make lists of which units can attack or defend, for spawn/placement menus.
            _attackerClasses = _unitTypes.Where( (uc) => uc.CanAttack ).ToList();
            _defenderClasses = _unitTypes.Where( (uc) => uc.CanDefend ).ToList();

            _cursorX = 0;
            _cursorY = 0;
            _paintEnabled = false;
            _terrainOverlayTiles = null;
            _showDefenderLOS = false;
            _showForbiddenTiles = false;

            if (this.PlayerView)
            {
                _mode = EditorMode.Defenders;
                _teamId = _playerViewTeamId;
            }
            else
            {
                _mode = EditorMode.Terrain;
                _teamId = _minimumTeamId;
            }

            _canvas.Init(_scenario.Terrain.Height+1);
            _canvas.UseColor = this.UseColor;
        }


        private void MoveCursor(int deltaX, int deltaY)
        {
            var oldX = _cursorX;
            var oldY = _cursorY;
            _cursorX = Math.Max(0, Math.Min(_scenario.Terrain.Width-1, _cursorX+deltaX));
            _cursorY = Math.Max(0, Math.Min(_scenario.Terrain.Height-1, _cursorY+deltaY));

            // If we're in terrain mode and painting is on, copy the tile value from the previous tile
            // to the new one.
            if (_paintEnabled && _mode==EditorMode.Terrain && (oldX != _cursorX || oldY != _cursorY))
            {
                var tileVal = _scenario.Terrain.GetTileValue(oldX, oldY);
                _scenario.Terrain.SetTileValue(_cursorX, _cursorY, tileVal);

                // Invalidate the LoS map.
                _terrainOverlayTiles = null;
            }
        }

        private void CycleMode()
        {
            var temp = _mode + 1;
            if (!Enum.IsDefined(typeof(EditorMode), temp))
                temp = 0;
            _mode = temp;
            _paintEnabled = false;

            RecalculateResourceCost();
        }

        private IList<UnitCharacteristics> LoadUnitsFile()
        {
            var fileContentsAsString = File.ReadAllText(_unitsFileName);
            var unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(fileContentsAsString);
            return unitsList;
        }

        private void GenerateTerrain()
        {
            var mapGenerator = new Generator(_mapGenOptions);
            _scenario.Terrain = mapGenerator.Create();

            // Invalidate all defense plans.  If the map resized, they might be out of bounds.
            // And even if not, odds are they're not placed anywhere useful for the new map.
            _scenario.DefensePlans.Clear();

            // Invalidate the LoS map.
            _terrainOverlayTiles = null;

            // Resize the canvas
            _canvas.Init(_scenario.Terrain.Height+1);
        }

        private void LoadOrCreateGeneratorOptions()
        {
            try
            {
                if (File.Exists(_optionsFile))
                {
                    var fileContentsAsString = File.ReadAllText(_optionsFile);
                    _mapGenOptions = JsonConvert.DeserializeObject<GeneratorOptions>(fileContentsAsString);
                }
            }
            catch (IOException ioe)
            {
                _logger.Warn(ioe, "Error loading GeneratorOptions file");
            }
            catch (JsonException je)
            {
                _logger.Warn(je, "Error loading GeneratorOptions file");
            }

            if (_mapGenOptions ==  null)
            {
                _mapGenOptions = new GeneratorOptions();
            }
        }

        private void SaveGeneratorOptions()
        {
            if (_mapGenOptions==null)
                return;

            try
            {
                var fileContentsAsString = JsonConvert.SerializeObject(_mapGenOptions, _jsonOpts);
                File.WriteAllText(_optionsFile, fileContentsAsString);
            }
            catch (IOException ioe)
            {
                _logger.Warn(ioe, "Error loading GeneratorOptions file");
                _statusMsg = ioe.Message;
            }
            catch (JsonException je)
            {
                _logger.Warn(je, "Error loading GeneratorOptions file");
                _statusMsg = je.Message;
            }
        }

        private void CycleTileType()
        {
            if (_cursorX<0 || _cursorY<0 || _cursorX>=_scenario.Terrain.Width || _cursorY>=_scenario.Terrain.Height)
                return;

            byte newTileVal = (byte)(_scenario.Terrain.GetTileValue(_cursorX, _cursorY) + 1);
            if (newTileVal >= _scenario.Terrain.TileTypes.Count)
                newTileVal = 0;
            _scenario.Terrain.SetTileValue(_cursorX, _cursorY, newTileVal);

            // Invalidate the LoS map.
            _terrainOverlayTiles = null;
        }

        private void ClearAllTiles()
        {
            _scenario.Terrain.Tiles = null;
        }

        private void PromptAndLoadScenario()
        {
            var prompt = String.IsNullOrEmpty(_lastScenarioFilename)?
                "Load file name: "
                : $"Load file name (enter for {_lastScenarioFilename}): ";
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt);

            try
            {
                var filename = String.IsNullOrEmpty(input)? _lastScenarioFilename : input;
                var fileContentsAsString = File.ReadAllText(filename);
                var newScenario = JsonConvert.DeserializeObject<Scenario>(fileContentsAsString);

                // TODO: validate new scenario?
                _scenario = newScenario;

                _lastScenarioFilename = filename;

                InitFromScenario();
            }
            catch (IOException ioe)
            {
                _statusMsg = ioe.Message;
            }
            catch (JsonException)
            {
                _statusMsg = "File is not a valid scenario";
            }
        }

        private void PromptAndSaveScenario()
        {
            var prompt = String.IsNullOrEmpty(_lastScenarioFilename)?
                "Save file name: "
                : $"Save file name (enter for {_lastScenarioFilename}): ";
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt);

            try
            {
                var filename = String.IsNullOrEmpty(input)? _lastScenarioFilename : input;
                var fileContentsAsString = JsonConvert.SerializeObject(_scenario, _jsonOpts);
                File.WriteAllText(filename, fileContentsAsString);

                _lastScenarioFilename = filename;
            }
            catch (IOException ioe)
            {
                _statusMsg = ioe.Message;
            }
        }

        private void WriteStatusMessage()
        {
            if (!String.IsNullOrEmpty(_statusMsg))
                _canvas.WriteText(_statusMsg, 0, _scenario.Terrain.Height, 0);
            else
                _canvas.WriteText("", 0, _scenario.Terrain.Height, 0);
        }

        private void CycleTeam()
        {
            _teamId += 1;
            if (_teamId>_maximumTeamId)
                _teamId = _minimumTeamId;
            RecalculateResourceCost();
        }

        private void DrawDefenderPlacements()
        {
            foreach (var plan in _scenario.DefensePlans)
            {
                _canvas.PaintDefensePlan(plan, _unitTypes, 0, 0);
            }
        }

        private void RemoveDefender()
        {
            var cursorPos = new Vector2Di(_cursorX, _cursorY);
            foreach (var plan in _scenario.DefensePlans)
                plan.Placements = plan.Placements.Where( (placement) => placement.Position!=cursorPos )
                    .ToList();

            // Invalidate the LOS map.  It will be rebuilt later.
            _terrainOverlayTiles = null;

            RecalculateResourceCost();
        }

        private void RemoveAllDefenders()
        {
            _scenario.DefensePlans = _scenario.DefensePlans.Where( (plan) => plan.TeamId!=_teamId )
                .ToList();

            // Invalidate the LOS map.  It will be rebuilt later.
            _terrainOverlayTiles = null;

            RecalculateResourceCost();
        }

        private void PlaceDefender(UnitCharacteristics unitClass)
        {
            RemoveDefender();

            var plan = _scenario.DefensePlans.FirstOrDefault( (dp) => dp.TeamId==_teamId );
            if (plan==null)
            {
                plan = new DefensePlan() { TeamId = _teamId, Placements = new List<DefenderPlacement>() };
                _scenario.DefensePlans.Add(plan);
            }

            var placement = new DefenderPlacement()
            {
                UnitType = unitClass.Name,
                Position = new Vector2Di(_cursorX, _cursorY)
            };
            plan.Placements.Add(placement);

            // Invalidate the LOS map.  It will be rebuilt later.
            _terrainOverlayTiles = null;

            RecalculateResourceCost();
        }

        private void ClearSpawnsAndGoalsForTeam()
        {
            _scenario.Terrain.SpawnPointsMap.Remove(_teamId);
            _scenario.Terrain.GoalPointsMap.Remove(_teamId);

            // Invalidate the terrain overlay map.  It will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        private void ClearOneSpawnOrGoal()
        {
            var cursorPos = new Vector2Di(_cursorX, _cursorY);
            IList<Vector2Di> spawnsForTeam = null;
            _scenario.Terrain.SpawnPointsMap.TryGetValue(_teamId, out spawnsForTeam);
            spawnsForTeam?.Remove(cursorPos);

            IList<Vector2Di> goalsForTeam = null;
            _scenario.Terrain.GoalPointsMap.TryGetValue(_teamId, out goalsForTeam);
            goalsForTeam?.Remove(cursorPos);

            // Invalidate the terrain overlay map.  It will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        private void AddOneSpawnPoint()
        {
            ClearOneSpawnOrGoal();
            IList<Vector2Di> spawnsForTeam = null;
            if (!_scenario.Terrain.SpawnPointsMap.TryGetValue(_teamId, out spawnsForTeam))
            {
                spawnsForTeam = new List<Vector2Di>();
                _scenario.Terrain.SpawnPointsMap[_teamId] = spawnsForTeam;
            }
            spawnsForTeam.Add(new Vector2Di(_cursorX, _cursorY));

            // Invalidate the terrain overlay map.  It will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        private void AddOneGoalPoint()
        {
            ClearOneSpawnOrGoal();
            IList<Vector2Di> goalsForTeam = null;
            if (!_scenario.Terrain.GoalPointsMap.TryGetValue(_teamId, out goalsForTeam))
            {
                goalsForTeam = new List<Vector2Di>();
                _scenario.Terrain.GoalPointsMap[_teamId] = goalsForTeam;
            }
            goalsForTeam.Add(new Vector2Di(_cursorX, _cursorY));

            // Invalidate the terrain overlay map.  It will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        private void PromptForSpawnDelay()
        {
            const int maxSaneTime = 1000;
            var prompt = $"Spawn Delay Time (enter for {_spawnTime}): ";
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt);

            if (!String.IsNullOrWhiteSpace(input))
            {
                int newTime = 0;
                int.TryParse(input, out newTime);
                if (newTime >= 0 && newTime < maxSaneTime)
                    _spawnTime = newTime;
            }
        }

        private void CycleSpawnPointIndex()
        {
            if (!_scenario.Terrain.SpawnPointsMap.ContainsKey(_teamId))
                _scenario.Terrain.SpawnPointsMap[_teamId] = new List<Vector2Di>();
            _selectedSpawnPointIndex += 1;
            if (_selectedSpawnPointIndex>=_scenario.Terrain.SpawnPointsMap[_teamId].Count)
                _selectedSpawnPointIndex = 0;
        }

        private void RemoveAttackersAtTime()
        {
            AttackPlan plan = _scenario.AttackPlans.FirstOrDefault( (ap) => ap.TeamId == _teamId);
            if (plan?.Spawns != null)
            {
                plan.Spawns = plan.Spawns
                    .Where( (sp) => (int)Math.Round(sp.Time)!=_spawnTime)
                    .ToList();
            }

            RecalculateResourceCost();
        }

        private void AddAttackerSpawn(UnitCharacteristics unitClass)
        {
            AttackPlan plan = _scenario.AttackPlans.FirstOrDefault( (ap) => ap.TeamId == _teamId);
            if (plan == null)
            {
                plan = new AttackPlan() { TeamId=_teamId };
                _scenario.AttackPlans.Add(plan);
            }

            var newUnit = new AttackerSpawn()
            {
                Time = _spawnTime,
                UnitType = unitClass.Name,
                SpawnPointIndex = _selectedSpawnPointIndex,
            };
            plan.Spawns.Add(newUnit);

            RecalculateResourceCost();
        }

        private void ClearAllAttackersForTeam()
        {
            AttackPlan plan = _scenario.AttackPlans.FirstOrDefault( (ap) => ap.TeamId == _teamId);
            plan?.Spawns.Clear();

            RecalculateResourceCost();
        }

        /// <summary>
        /// Rebuilds the terrain overlay grid.  This changes the background color of tiles to show
        /// defenders' line-of-sight, or tiles that ruin eligibility for challenges.
        /// </summary>
        private void BuildTerrainOverlay()
        {
            _terrainOverlayTiles = new int[_scenario.Terrain.Width, _scenario.Terrain.Height];

            if (_showForbiddenTiles)
                BuildForbiddenTilesMap();
            if (_showDefenderLOS)
                BuildDefenderLOSMap();
        }

        /// <summary>
        /// Enumerates all of the tiles within a certain Euclidean distance of a given point.
        /// </summary>
        private IEnumerable<Vector2Di> TilesInCircleAround(Vector2Di center, double dist)
        {
            var minX = Math.Max(0, (int)Math.Round(center.X - dist));
            var minY = Math.Max(0, (int)Math.Round(center.Y - dist));
            var maxX = Math.Min(_scenario.Terrain.Width-1, (int)Math.Round(center.X + dist));
            var maxY = Math.Min(_scenario.Terrain.Height-1, (int)Math.Round(center.Y + dist));

            for (var y=minY; y<=maxY; ++y)
            {
                for (var x=minX; x<=maxX; ++x)
                {
                    var pos = new Vector2Di(x, y);
                    if (pos.DistanceTo(center)<dist)
                        yield return pos;
                }
            }
        }

        private void BuildForbiddenTilesMap()
        {
            if (_scenario.Challenges == null || _scenario.Challenges.Count == 0)
                return;

            var minSpawnDist = _scenario.Challenges.Select( (chal) => chal.MinimumDistFromSpawnPts ).Max();
            var minGoalDist = _scenario.Challenges.Select( (chal) => chal.MinimumDistFromGoalPts ).Max();

            var allSpawns = _scenario.Terrain.SpawnPointsMap.Values.SelectMany( (list) => list );
            var allGoals = _scenario.Terrain.GoalPointsMap.Values.SelectMany( (list) => list );

            const int forbiddenTeamColor = -1;
            foreach (var spawnPt in allSpawns)
            {
                foreach (var pt in TilesInCircleAround(spawnPt, minSpawnDist))
                    _terrainOverlayTiles[pt.X, pt.Y] = forbiddenTeamColor;
            }
            foreach (var spawnPt in allGoals)
            {
                foreach (var pt in TilesInCircleAround(spawnPt, minGoalDist))
                    _terrainOverlayTiles[pt.X, pt.Y] = forbiddenTeamColor;
            }
        }

        /// <summary>
        /// Builds a grid indicating which tiles are visible and in weapon range from currently placed defenders.
        /// </summary>
        private void BuildDefenderLOSMap()
        {
            foreach (var plan in _scenario.DefensePlans)
            {
                foreach (var defPlacement in plan.Placements)
                {
                    var unitClass = _unitTypes.FirstOrDefault( (ut) => ut.Name==defPlacement.UnitType);
                    var range = unitClass.WeaponRangeTiles;
                    if (unitClass != null && unitClass.WeaponDamage>0 && range>0)
                    {
                        var minX = Math.Max(0, (int)Math.Round(defPlacement.Position.X - range));
                        var minY = Math.Max(0, (int)Math.Round(defPlacement.Position.Y - range));
                        var maxX = Math.Min(_scenario.Terrain.Width-1, (int)Math.Round(defPlacement.Position.X + range));
                        var maxY = Math.Min(_scenario.Terrain.Height-1, (int)Math.Round(defPlacement.Position.Y + range));

                        for (var y=minY; y<=maxY; ++y)
                        {
                            for (var x=minX; x<=maxX; ++x)
                            {
                                var pos = new Vector2Di(x, y);
                                if (pos.DistanceTo(defPlacement.Position)>range)
                                    continue;

                                var currentVal = _terrainOverlayTiles[x,y];

                                // Using -1 to signify multiple teams have LOS on this spot.
                                if (currentVal==-1)
                                    continue;

                                if (currentVal != plan.TeamId)
                                {
                                    var visible = _scenario.Terrain.HasLineOfSight(defPlacement.Position, pos);
                                    if (visible && currentVal==0)
                                        _terrainOverlayTiles[x,y] = plan.TeamId;
                                    else if (visible)
                                        _terrainOverlayTiles[x,y] = -1;
                                }
                            }
                        }

                        const int overlapTeamColor = -1;
                        foreach (var pos in TilesInCircleAround(defPlacement.Position, range))
                        {
                            var currentVal = _terrainOverlayTiles[pos.X,pos.Y];
                            if (currentVal != plan.TeamId)
                            {
                                var visible = _scenario.Terrain.HasLineOfSight(defPlacement.Position, pos);
                                if (visible && currentVal==0)
                                    _terrainOverlayTiles[pos.X,pos.Y] = plan.TeamId;
                                else if (visible)
                                    _terrainOverlayTiles[pos.X,pos.Y] = overlapTeamColor;
                            }
                        }
                    }
                }
            }
        }

        private int EditPoco<T>(T obj)
        {
            int row = 0;
            _canvas.ClearScreen();

            _canvas.WriteTextDirect($"Edit values for {obj.GetType().Name} -", 0, row++);

            var props = obj.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (!typeof(IConvertible).IsAssignableFrom(prop.PropertyType))
                    continue;

                var curVal = Convert.ToString(prop.GetValue(obj));
                var prompt = $"  {prop.Name} (enter for {curVal}):";
                var input = _canvas.PromptForInput(0, row++, prompt);

                if (!String.IsNullOrWhiteSpace(input))
                {
                    var newVal = Convert.ChangeType(input, prop.PropertyType);
                    prop.SetValue(obj, newVal);
                }
            }

            return row;
        }

        private void PromptToEditGeneratorOptions()
        {
            EditPoco(_mapGenOptions);
            SaveGeneratorOptions();
        }

        private void RecalculateResourceCost()
        {
            _totalResourceCost = 0;

            switch (_mode)
            {
                case EditorMode.Attackers:
                {
                    var plans = _scenario.AttackPlans.Where( (ap) => ap.TeamId == _teamId );
                    var spawnList = plans.SelectMany( (ap) => ap.Spawns );
                    foreach (var spawn in spawnList)
                    {
                        var unitChars = _attackerClasses.FirstOrDefault( (cls) => cls.Name == spawn.UnitType );
                        _totalResourceCost += (unitChars!=null)? unitChars.ResourceCost : 0;
                    }
                    break;
                }
                case EditorMode.Defenders:
                {
                    var plans = _scenario.DefensePlans.Where( (dp) => dp.TeamId == _teamId );
                    var placementList = plans.SelectMany( (dp) => dp.Placements );
                    foreach (var placement in placementList)
                    {
                        var unitChars = _defenderClasses.FirstOrDefault( (cls) => cls.Name == placement.UnitType );
                        _totalResourceCost += (unitChars!=null)? unitChars.ResourceCost : 0;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Checks that the terminal is big enough for the editor.  If it's not, prints an message
        /// and returns false.  If it is big enough, returns true.
        /// </summary>
        private bool TestScreenSize(Terrain terrain)
        {
            var minX = terrain.Width;
            var minY = terrain.Height+1;
            var suggestedX = minX + 20;
            var suggestedY = minY;

            var screen = _canvas.GetDisplaySize();
            if (screen.X<minX || screen.Y<minY)
            {
                var msg = "Your terminal window is too small for this scenario.\n"
                    + $"Minimum=({minX}, {minY}); recommended=({suggestedX}, {suggestedY})";

                _logger.Warn(msg);
                Console.WriteLine(msg);
                Console.WriteLine();

                Console.Write("Press any key to exit");
                Console.ReadKey();

                return false;
            }

            return true;
        }

        private void CheckForErrors()
        {
            var terrainErrors = Resolver.Validator.FindTerrainErrors(_scenario.Terrain);
            var doTerrainErrorsExist = terrainErrors.Any();

            var attackerErrors = Enumerable.Empty<string>();
            var defenderErrors = Enumerable.Empty<string>();

            if (!doTerrainErrorsExist)
            {
                attackerErrors = Resolver.Validator.FindAttackPlanErrors(_scenario.Terrain, _unitTypes, _scenario.AttackPlans);
                defenderErrors = Resolver.Validator.FindDefensePlanErrors(_scenario.Terrain, _unitTypes, _scenario.DefensePlans);
            }

            var doAttackerErrorsExist = attackerErrors.Any();
            var doDefenderErrorsExist = defenderErrors.Any();

            var challengeErrors = new List<IEnumerable<string>>();
            if (!doTerrainErrorsExist && !doAttackerErrorsExist && !doDefenderErrorsExist && _scenario.Challenges != null)
                challengeErrors = _scenario.Challenges
                    .Select( (ch) => Resolver.Validator.GetChallengeDisqualifiers(_scenario, null, _unitTypes, ch) )
                    .ToList();

            var doChallengeErrorsExist = challengeErrors.SelectMany( (chList) => chList ).Any();

            if (!(doTerrainErrorsExist | doAttackerErrorsExist | doDefenderErrorsExist | doChallengeErrorsExist))
            {
                // If nothing's wrong, just say so on the status bar.
                _statusMsg = "No errors";
                return;
            }

            // Clear the screen and print a list of errors.
            int row = 0;
            _canvas.ClearScreen();

            if (doTerrainErrorsExist)
            {
                _canvas.WriteTextDirect("Terrain Errors:", 0, row++);
                foreach (var errMsg in terrainErrors)
                    _canvas.WriteTextDirect("  * " + errMsg, 0, row++);
                row += 1;
            }

            if (doAttackerErrorsExist)
            {
                _canvas.WriteTextDirect("Attacker Errors:", 0, row++);
                foreach (var errMsg in attackerErrors)
                    _canvas.WriteTextDirect("  * " + errMsg, 0, row++);
                row += 1;
            }

            if (doDefenderErrorsExist)
            {
                _canvas.WriteTextDirect("Defender Errors:", 0, row++);
                foreach (var errMsg in defenderErrors)
                    _canvas.WriteTextDirect("  * " + errMsg, 0, row++);
                row += 1;
            }

            for (var i=0; i<challengeErrors.Count; ++i)
            {
                if (challengeErrors[i].Any())
                {
                    _canvas.WriteTextDirect($"Challenge {_scenario.Challenges[i].Name} Disqualifiers:", 0, row++);
                    foreach (var errMsg in challengeErrors[i])
                        _canvas.WriteTextDirect("  * " + errMsg, 0, row++);
                    row += 1;
                }
            }

            // Wait for the user to press a key.  We don't care what key it is, unless it's
            // control-c, in which case we should honor their desire to quit.
            row++;
            _canvas.WriteTextDirect("Press a key to continue", 0, row++);

            var keyInfo = _canvas.ReadKey();
            if (keyInfo.Key==ConsoleKey.C && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                _exitEditor = true;
        }


        private void WriteModeHelpChallenges(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Challenges);

            row += 1;
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText("(D) set spawn dist all", col, row++, 0);
            _canvas.WriteText("(G) set goal dist all", col, row++, 0);
            _canvas.WriteText("(+) add challenge", col, row++, 0);
            _canvas.WriteText("(-) remove challenge", col, row++, 0);

            row += 1;
            for (int i=0; i<_scenario.Challenges.Count; ++i)
                _canvas.WriteText($"({i+1}) edit \"{_scenario.Challenges[i].Name}\"", col, row++, 0);

            row += 1;
        }

        private void ProcessKeyChallengesMode(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Backspace:
                    ClearAllChallenges();
                    return;
            }

            switch (keyInfo.KeyChar)
            {
                case '+':
                    AddChallenge();
                    return;
                case '-':
                    RemoveChallenge();
                    return;
                case 'd':
                    SetAllChallengeSpawnDistance();
                    return;
                case 'g':
                    SetAllChallengeGoalDistance();
                    return;
            }

            // TODO: add set-minimum-distance-from-spawn button

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=1 && keyNumberValue<=_scenario.Challenges.Count)
            {
                EditChallenge(keyNumberValue-1);
            }
        }

        private void ClearAllChallenges()
        {
            _scenario.Challenges.Clear();
        }

        private void RemoveChallenge()
        {
            if (_scenario.Challenges.Count>0)
                _scenario.Challenges.RemoveAt(_scenario.Challenges.Count-1);
        }

        private void AddChallenge()
        {
            DefenderChallenge newChallenge = null;
            if (_scenario.Challenges.Count>0)
            {
                // Just clone the last one
                var oldChallenge = _scenario.Challenges[_scenario.Challenges.Count-1];
                newChallenge = oldChallenge.Clone();
            }
            else
            {
                newChallenge = new DefenderChallenge()
                {
                    PlayerTeamId = 2,
                    MinimumDistFromSpawnPts = 8,
                    MinimumDistFromGoalPts = 2,
                    MaximumResourceCost = 1000,
                    MaximumTotalUnitCount = 100,
                    MaximumDefendersLostCount = 100,
                    AttackersMustNotReachGoal = true,
                };
            }

            // Default name is "*", "**", "***", etc., depending on how many exist.
            newChallenge.Name = new string('*', _scenario.Challenges.Count+1);
            _scenario.Challenges.Add(newChallenge);
        }

        private void EditChallenge(int challengeIndex)
        {
            if (challengeIndex<0 || challengeIndex>=_scenario.Challenges.Count)
                return;

            var challenge = _scenario.Challenges[challengeIndex];
            int row = EditPoco(challenge);

            var maxUnitTypes = new Dictionary<string,int>();
            foreach (var unitType in _defenderClasses)
            {
                string oldValString = "no limit";
                if (challenge.MaximumUnitTypeCount.ContainsKey(unitType.Name))
                    oldValString = challenge.MaximumUnitTypeCount[unitType.Name].ToString();

                var prompt = $"  Maximum number of {unitType.Name} (enter for {oldValString}, 'x' for no limit): ";
                var input = _canvas.PromptForInput(0, row++, prompt);

                var newValString = (string.IsNullOrWhiteSpace(input))? oldValString : input;

                int newVal = 0;
                if (int.TryParse(newValString, out newVal))
                    maxUnitTypes[unitType.Name] = newVal;
                else
                    maxUnitTypes.Remove(unitType.Name);
            }

            challenge.MaximumUnitTypeCount = maxUnitTypes;
        }

        private void SetDefaultStatusBar()
        {
            var buff = new System.Text.StringBuilder();

            // Cursor position
            buff.Append('(').Append(_cursorX).Append(',').Append(_cursorY).Append(") ");

            bool errors = Validator.FindTerrainErrors(_scenario.Terrain).Any();
            errors |= Validator.FindAttackPlanErrors(_scenario.Terrain, _unitTypes, _scenario.AttackPlans).Any();
            errors |= Validator.FindDefensePlanErrors(_scenario.Terrain, _unitTypes, _scenario.DefensePlans).Any();

            if (errors)
            {
                buff.Append("ERRORS - press C for details");
            }
            else if (_scenario.Challenges!=null && _scenario.Challenges.Count>0)
            {
                buff.Append("Eligible: ");
                var eligList = new List<string>();
                foreach (var chal in _scenario.Challenges)
                {
                    var eligible = !Validator.GetChallengeDisqualifiers(_scenario, null, _unitTypes, chal).Any();
                    if (eligible)
                        buff.Append(chal.Name).Append(' ');
                }
            }

            _statusMsg = buff.ToString();
        }

        /// <summary>
        /// If spawn points and challenges exist, the cursor's distance to the closest spawn point is
        /// set as the minimum spawn distance for all challenges.
        /// </summary>
        private void SetAllChallengeSpawnDistance()
        {
            if (_scenario.Terrain.SpawnPointsMap==null || _scenario.Challenges==null || _scenario.Challenges.Count==0)
                return;

            var cursorPos = new Vector2Di(_cursorX, _cursorY);
            var allSpawnPts = _scenario.Terrain.SpawnPointsMap.Values.SelectMany( (list) => list );
            if (!allSpawnPts.Any())
                return;

            var closestDist = (int)Math.Floor(allSpawnPts.Select( (spPt) => spPt.DistanceTo(cursorPos) ).Min());

            _logger.Debug("Setting all challenge MinimumDistFromSpawnPts to {0}", closestDist);
            foreach (var chal in _scenario.Challenges)
                chal.MinimumDistFromSpawnPts = closestDist;

            // Invalidate the terrain overlay so it will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        /// <summary>
        /// If goal points and challenges exist, the cursor's distance to the closest goal point is
        /// set as the minimum goal distance for all challenges.
        /// </summary>
        private void SetAllChallengeGoalDistance()
        {
            if (_scenario.Terrain.GoalPointsMap==null || _scenario.Challenges==null || _scenario.Challenges.Count==0)
                return;

            var cursorPos = new Vector2Di(_cursorX, _cursorY);
            var allGoalPts = _scenario.Terrain.GoalPointsMap.Values.SelectMany( (list) => list );
            if (!allGoalPts.Any())
                return;

            var closestDist = (int)Math.Floor(allGoalPts.Select( (spPt) => spPt.DistanceTo(cursorPos) ).Min());

            _logger.Debug("Setting all challenge MinimumDistFromGoalPts to {0}", closestDist);
            foreach (var chal in _scenario.Challenges)
                chal.MinimumDistFromGoalPts = closestDist;

            // Invalidate the terrain overlay so it will be rebuilt later.
            _terrainOverlayTiles = null;
        }

        #region UIPages

        private void WriteModeHelp()
        {
            if (this.PlayerView)
                WritePlayerViewHelp();
            else
                WriteEditorModeHelp();
        }

        private void ProcessUserInput()
        {
            // Wait for a key press.
            var keyInfo = _canvas.ReadKey();

            // If the shift key is held and an arrow is pressed, we want to move a large distance.
            // Sadly, this is of limited use on MacOS - it doesn't give us modifiers for up and down arrows,
            // for some reason.
            int moveDist = ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)? 10 : 1;

            // First see if we can match on the key-code, for special keys like arrows.
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    MoveCursor(0, -moveDist);
                    return;
                case ConsoleKey.DownArrow:
                    MoveCursor(0, +moveDist);
                    return;
                case ConsoleKey.LeftArrow:
                    MoveCursor(-moveDist, 0);
                    return;
                case ConsoleKey.RightArrow:
                    MoveCursor(+moveDist, 0);
                    return;
                case ConsoleKey.Escape:
                    _exitEditor = true;
                    break;
                case ConsoleKey.Tab:
                    _showForbiddenTiles = !_showForbiddenTiles;
                    _terrainOverlayTiles = null;
                    return;
                case ConsoleKey.C:
                    // Intercept Ctrl-C nicely.
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                        _exitEditor = true;
                    else
                        CheckForErrors();
                    return;
            }

            // Now try to match on regular characters.  These aren't all represented in the ConsoleKey
            // enum above.  Note that KeyChar gives you the proper shifted or ctrl'd ASCII code, so
            // Shift-s is 'S'.
            switch (keyInfo.KeyChar)
            {
                case 'l':
                    PromptAndLoadScenario();
                    return;
                case 's':
                    PromptAndSaveScenario();
                    return;
                case '`':
                    this.UseColor = !this.UseColor;
                    _canvas.UseColor = this.UseColor;
                    return;
                case 'v':
                    _playAfterExit = true;
                    _exitEditor = true;
                    return;
            }

            if (this.PlayerView)
                ProcessKeyPlayerView(keyInfo);
            else
                ProcessEditorUserInput(keyInfo);
        }

        // -- Editor View ---
        private void WriteEditorModeHelp()
        {
            int row = 0;
            int col = _scenario.Terrain.Width + 2;
            _canvas.WriteText("(Enter) mode:", col, row++, 0);
            _canvas.WriteText($"{_mode}", col, row++, 0);

            switch (_mode)
            {
                case EditorMode.Terrain:
                    WriteModeHelpTerrain(col, ref row);
                    break;
                case EditorMode.Defenders:
                    WriteModeHelpDefenders(col, ref row);
                    break;
                case EditorMode.SpawnsAndGoals:
                    WriteModeHelpSpawnsAndGoals(col, ref row);
                    break;
                case EditorMode.Challenges:
                    WriteModeHelpChallenges(col, ref row);
                    break;
                case EditorMode.Attackers:
                    WriteModeHelpAttackers(col, ref row);
                    break;
                default:
                    _canvas.WriteText("Not implemented", col, row++, 0);
                    break;
            }

            row += 1;
            _canvas.WriteText("(`) toggle color", col, row++, 0);
            _canvas.WriteText("(Tab) toggle forbidden", col, row++, 0);
            _canvas.WriteText("(L) load scenario", col, row++, 0);
            _canvas.WriteText("(S) save scenario", col, row++, 0);
            _canvas.WriteText("(C) check for errors", col, row++, 0);
            _canvas.WriteText("(V) view resolution", col, row++, 0);
            _canvas.WriteText("(ESC) exit", col, row++, 0);
        }

        private void ProcessEditorUserInput(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Enter:
                    CycleMode();
                    return;
            }

            // If we didn't return before now, look for mode-specific key handling.
            switch (_mode)
            {
                case EditorMode.Terrain:
                    ProcessKeyTerrainMode(keyInfo);
                    return;
                case EditorMode.Defenders:
                    ProcessKeyDefendersMode(keyInfo);
                    return;
                case EditorMode.SpawnsAndGoals:
                    ProcessKeySpawnsAndGoalsMode(keyInfo);
                    return;
                case EditorMode.Challenges:
                    ProcessKeyChallengesMode(keyInfo);
                    return;
                case EditorMode.Attackers:
                    ProcessKeyAttackersMode(keyInfo);
                    return;
            }
        }

        private void WriteModeHelpTerrain(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Terrain);

            var paintModeIndicator = _paintEnabled? "on" : "off";

            row += 1;
            _canvas.WriteText("(Space) change Tile", col, row++, 0);

            row += 1;
            for (int i=0; i<_scenario.Terrain.TileTypes.Count; ++i)
            {
                var name = _scenario.Terrain.TileTypes[i].Name;
                _canvas.WriteText($"({i+1}) place {name}", col, row++, 0);
            }

            row += 1;
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText($"(P) toggle paint mode ({paintModeIndicator})", col, row++, 0);
            _canvas.WriteText("(R) randomly generate", col, row++, 0);
            _canvas.WriteText("(O) generator options", col, row++, 0);
        }

        private void ProcessKeyTerrainMode(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Spacebar:
                    CycleTileType();
                    return;
                case ConsoleKey.Backspace:
                    ClearAllTiles();
                    return;
                case ConsoleKey.P:
                    _paintEnabled = !_paintEnabled;
                    return;
                case ConsoleKey.R:
                    GenerateTerrain();
                    return;
                case ConsoleKey.O:
                    PromptToEditGeneratorOptions();
                    return;
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=1 && keyNumberValue<=_scenario.Terrain.TileTypes.Count)
            {
                _scenario.Terrain.SetTileValue(_cursorX, _cursorY, (byte)(keyNumberValue-1));

                // Invalidate the LoS map.
                _terrainOverlayTiles = null;
            }
        }

        private void WriteModeHelpDefenders(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Defenders);

            _canvas.WriteText($"(T) Team {_teamId}", col, row++, _teamId);

            row += 1;
            _canvas.WriteText("(\\) toggle LoS", col, row++, 0);
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText("(1) none", col, row++, 0);

            for (int i=0; i<_defenderClasses.Count; ++i)
                _canvas.WriteText($"({i+2}) {_defenderClasses[i].Name}", col, row++, 0);

            row += 1;
            _canvas.WriteText($"Total Cost: {_totalResourceCost}", col, row++, 0);
        }

        private void ProcessKeyDefendersMode(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.T:
                    CycleTeam();
                    return;
                case ConsoleKey.Backspace:
                    RemoveAllDefenders();
                    return;
            }

            switch (keyInfo.KeyChar)
            {
                case '1':
                    RemoveDefender();
                    return;
                case '\\':
                    _showDefenderLOS = !_showDefenderLOS;
                    _terrainOverlayTiles = null;
                    return;
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=2 && keyNumberValue<=_defenderClasses.Count+1)
            {
                PlaceDefender(_defenderClasses[keyNumberValue-2]);
            }
        }

        private void WriteModeHelpSpawnsAndGoals(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.SpawnsAndGoals);

            _canvas.WriteText($"(T) team {_teamId}", col, row++, _teamId);

            row += 1;
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText("(1) remove", col, row++, 0);
            _canvas.WriteText("(2) add spawn O", col, row++, 0);
            _canvas.WriteText("(3) add goal X", col, row++, 0);
        }

        private void ProcessKeySpawnsAndGoalsMode(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.T:
                    CycleTeam();
                    return;
                case ConsoleKey.Backspace:
                    ClearSpawnsAndGoalsForTeam();
                    return;
            }

            switch (keyInfo.KeyChar)
            {
                case '1':
                    ClearOneSpawnOrGoal();
                    return;
                case '2':
                    AddOneSpawnPoint();
                    return;
                case '3':
                    AddOneGoalPoint();
                    return;
            }
        }

        private void WriteModeHelpAttackers(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Attackers);

            if (_scenario.AttackPlans==null)
                _scenario.AttackPlans = new List<AttackPlan>();
            AttackPlan plan = _scenario.AttackPlans.FirstOrDefault( (ap) => ap.TeamId == _teamId);
            if (plan == null)
            {
                plan = new AttackPlan() { TeamId = _teamId };
                _scenario.AttackPlans.Add(plan);
            }

            var hasSpawnPoint = _scenario.Terrain.SpawnPointsMap.ContainsKey(_teamId)
                && (_selectedSpawnPointIndex>=0 && _selectedSpawnPointIndex<_scenario.Terrain.SpawnPointsMap[_teamId].Count);

            var spawnPointText = (hasSpawnPoint)? _scenario.Terrain.SpawnPointsMap[_teamId][_selectedSpawnPointIndex].ToString()
                : "none";

            // Make a list of all spawn times currently in use.
            var existingSpawnTimes = plan.Spawns.Select( (sp) => sp.Time )
                .Distinct()
                .OrderBy( (time) => time )
                .ToList();
            var existingTimesStr = String.Join(", ", existingSpawnTimes);

            // Basic selections: team, spawn point delay, spawn point index.
            _canvas.WriteText($"(T) team {_teamId}", col, row++, _teamId);
            _canvas.WriteText($"(D) spawn delay time {_spawnTime}", col, row++, 0);
            _canvas.WriteText("  " + existingTimesStr, col, row++, 0);
            _canvas.WriteText($"(P) spawn point {spawnPointText}" , col, row++, 0);

            if (hasSpawnPoint)
            {
                row += 1;
                _canvas.WriteText("(Backspace) clear all", col, row++, 0);
                _canvas.WriteText($"(1) clear for this time", col, row++, 0);

                // Write a list of all available attacker types.  But the trick part is, we also
                // want to include the count for that unit type already assigned.
                // This obviously breaks if we have >7 attacker types.
                for (int i=0; i<_attackerClasses.Count; ++i)
                {
                    Func<AttackerSpawn,bool> matchFunc = (sp) =>
                    {
                        return ((int)Math.Round(sp.Time))==_spawnTime
                            && sp.SpawnPointIndex == _selectedSpawnPointIndex
                            && sp.UnitType == _attackerClasses[i].Name;
                    };

                    var count = plan.Spawns.Where( (matchFunc) ).Count();
                    _canvas.WriteText($"({i+2}) Add {_attackerClasses[i].Name} {count}", col, row++, 0);
                }

                row += 1;
                _canvas.WriteText($"Total Cost: {_totalResourceCost}", col, row++, 0);
            }
            else
            {
                row += 1;
                _canvas.WriteText($"Please select a spawn point", col, row++, _teamId);
            }
        }

        private void ProcessKeyAttackersMode(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.T:
                    CycleTeam();
                    return;
                case ConsoleKey.D:
                    PromptForSpawnDelay();
                    return;
                case ConsoleKey.P:
                    CycleSpawnPointIndex();
                    return;
                case ConsoleKey.Backspace:
                    ClearAllAttackersForTeam();
                    return;
            }

            switch (keyInfo.KeyChar)
            {
                case '1':
                    RemoveAttackersAtTime();
                    return;
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=2 && keyNumberValue<=_attackerClasses.Count+1)
            {
                AddAttackerSpawn(_attackerClasses[keyNumberValue-2]);
            }
        }


        // ---Player view---

        private void WritePlayerViewHelp()
        {
            Debug.Assert(this.PlayerView);

            int col = _scenario.Terrain.Width + 2;
            int row = 0;

            _canvas.WriteText("Place defenders", col, row++, _playerViewTeamId);
            row += 1;

            _canvas.WriteText("(H) show help screen", col, row++, 0);
            _canvas.WriteText("(Arrow keys) move cursor", col, row++, 0);
            _canvas.WriteText("(\\) toggle LoS", col, row++, 0);
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText("(1) none", col, row++, 0);

            for (int i=0; i<_defenderClasses.Count; ++i)
                _canvas.WriteText($"({i+2}) {_defenderClasses[i].Name}", col, row++, 0);

            row += 1;
            _canvas.WriteText($"Total Cost: {_totalResourceCost}", col, row++, 0);

            row += 1;
            _canvas.WriteText("(`) toggle color", col, row++, 0);
            _canvas.WriteText("(Tab) toggle forbidden", col, row++, 0);
            _canvas.WriteText("(L) load game", col, row++, 0);
            _canvas.WriteText("(S) save game", col, row++, 0);
            _canvas.WriteText("(C) check for errors", col, row++, 0);
            _canvas.WriteText("(V) view resolution", col, row++, 0);
            _canvas.WriteText("(ESC) exit", col, row++, 0);
        }

        private void ProcessKeyPlayerView(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Backspace:
                    RemoveAllDefenders();
                    return;
            }

            switch (keyInfo.KeyChar)
            {
                case 'h':
                    ShowFullScreenPlayerHelp();
                    return;
                case '1':
                    RemoveDefender();
                    return;
                case '\\':
                    _showDefenderLOS = !_showDefenderLOS;
                    _terrainOverlayTiles = null;
                    return;
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=2 && keyNumberValue<=_defenderClasses.Count+1)
            {
                PlaceDefender(_defenderClasses[keyNumberValue-2]);
            }
        }

        private void ShowFullScreenPlayerHelp()
        {
            _canvas.ClearScreen();

            int row = 0;

            _canvas.WriteTextDirect("Intro", 0, row++);
            _canvas.WriteTextDirect(" * Place defenders to stop the enemy team from reaching the goal.", 0, row++);
            _canvas.WriteTextDirect(" * Press V to see the battle results.", 0, row++);
            _canvas.WriteTextDirect(" * Beat challenges by using fewer resources and avoiding forbidden areas.", 0, row++);

            row += 1;
            _canvas.WriteTextDirect("Terrain", 0, row++);
            _canvas.WriteTextDirect("   Open - Units can see and move through here.", 0, row++);
            _canvas.WriteTextDirect(" : Stone - Units can't see or move through stone.", 0, row++);
            _canvas.WriteTextDirect(" ~ Water - Prevents movement but not vision or attacks.", 0, row++);
            _canvas.WriteTextDirect(" @ Fog - Reduces vision and attack range to 1 tile, but doesn't prevent movement.", 0, row++);

            row += 1;
            _canvas.WriteTextDirect("Attackers", 0, row++);
            _canvas.WriteTextDirect(" T Grunt - Moves toward the goal, but attacks things in the way.", 0, row++);
            _canvas.WriteTextDirect(" i Scout - Tries to avoid defenders while moving toward the goal.", 0, row++);
            _canvas.WriteTextDirect(" ] Crossbowman - Moves toward the goal, but attacks at range along the way.", 0, row++);
            _canvas.WriteTextDirect(" Y Berserker - Attacks any enemy it sees.", 0, row++);
            _canvas.WriteTextDirect(" & Assassin - Chooses the best approach to attack enemies it seees.", 0, row++);

            row += 1;
            _canvas.WriteTextDirect("Defenders (all immobile)", 0, row++);
            _canvas.WriteTextDirect(" { Archer - Attacks any enemies at long range.", 0, row++);
            _canvas.WriteTextDirect(" ^ Pikeman - Attacks enemies within 2 tiles.", 0, row++);
            _canvas.WriteTextDirect(" # Barricade - Doesn't attack, but blocks enemy movement until destroyed.", 0, row++);

            // Wait for the user to press a key.  We don't care what key it is, unless it's
            // control-c, in which case we should honor their desire to quit.
            row++;
            _canvas.WriteTextDirect("Press a key to continue", 0, row++);

            var keyInfo = _canvas.ReadKey();
            if (keyInfo.Key==ConsoleKey.C && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                _exitEditor = true;
        }

        #endregion UIPages
    }
}
