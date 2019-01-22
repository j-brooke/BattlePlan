using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using BattlePlan.Common;
using BattlePlan.MapGeneration;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Interactive terminal-based tool for editing maps and scenarios.
    /// </summary>
    public class LowEffortEditor
    {
        public LowEffortEditor()
        {
            LoadOrCreateGeneratorOptions();
        }

        public void EditScenario(string filename)
        {
            // TODO: clean up various load scenarios

            if (!String.IsNullOrWhiteSpace(filename))
            {
                var fileContentsAsString = File.ReadAllText(filename);
                var newScenario = JsonConvert.DeserializeObject<Scenario>(fileContentsAsString);

                _lastScenarioFilename = filename;
                EditScenario(newScenario);
            }
            else
            {
                EditScenario((Scenario)null);
            }
        }

        public void EditScenario(Scenario foo)
        {
            // TODO: Check terminal height/width and do something if they're too small.

            _scenario = foo;
            if (_scenario == null)
            {
                _scenario = new Scenario()
                {
                    UnitTypes = LoadUnitsFile(),
                };
            }
            if (_scenario.Terrain == null)
                _scenario.Terrain = Terrain.NewDefault();
            InitFromScenario();

            _canvas.Init();

            _exitEditor = false;
            while (!_exitEditor)
            {
                // If we're supposed to show defenders' range/line-of-site regions, make sure we've
                // got the grid built.  We may have deleted it when we added/removed defenders.
                if (_mode==EditorMode.Defenders && _showDefenderLOS && _defenderLOSTiles==null)
                    BuildDefenderLOSMap();

                var terrainOverride = (_mode==EditorMode.Defenders && _showDefenderLOS)? _defenderLOSTiles : null;

                // Draw the map and everything on it.
                _canvas.BeginFrame();
                WriteModeHelp();
                _canvas.PaintTerrain(_scenario.Terrain, terrainOverride, 0, 0);
                _canvas.PaintSpawnPoints(_scenario.Terrain, 0, 0);
                _canvas.PaintGoalPoints(_scenario.Terrain, 0, 0);
                DrawDefenderPlacements();
                WriteStatusMessage();
                _canvas.EndFrame();

                // Clear the status message after showing it once.
                _statusMsg = null;

                _canvas.ShowCursor(_cursorX, _cursorY);
                ProcessUserInput();
            }

            _canvas.Shutdown();
        }
        private const int _minimumTeamId = 1;
        private const int _maximumTeamId = 2;

        private const string _unitsFileName = "resources/units.json";

        private const string _optionsFile = ".options.json";

        private readonly LowEffortCanvas _canvas = new LowEffortCanvas();
        private GeneratorOptions _mapGenOptions;
        private Scenario _scenario;
        private int _cursorX;
        private int _cursorY;
        private bool _exitEditor;
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
        private int[,] _defenderLOSTiles;


        /// <summary>
        /// Called on start, or when a new scenario is loaded.  Rebuilds internal data and such.
        /// </summary>
        private void InitFromScenario()
        {
            if (_scenario.UnitTypes == null)
                _scenario.UnitTypes = LoadUnitsFile();

            // Make lists of which units can attack or defend, for spawn/placement menus.
            _attackerClasses = _scenario.UnitTypes.Where( (uc) => uc.CanAttack ).ToList();
            _defenderClasses = _scenario.UnitTypes.Where( (uc) => uc.CanDefend ).ToList();

            _cursorX = 0;
            _cursorY = 0;
            _mode = EditorMode.Terrain;
            _teamId = _minimumTeamId;
            _paintEnabled = false;
            _defenderLOSTiles = null;
            _showDefenderLOS = false;
        }

        private void ProcessUserInput()
        {
            // Wait for a key press.
            var keyInfo = _canvas.ReadKey();

            // First see if we can match on the key-code, for special keys like arrows.
            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                    MoveCursor(0, -1);
                    return;
                case ConsoleKey.DownArrow:
                    MoveCursor(0, +1);
                    return;
                case ConsoleKey.LeftArrow:
                    MoveCursor(-1, 0);
                    return;
                case ConsoleKey.RightArrow:
                    MoveCursor(+1, 0);
                    return;
                case ConsoleKey.Escape:
                    _exitEditor = true;
                    break;
                case ConsoleKey.Enter:
                    CycleMode();
                    return;
                case ConsoleKey.C:
                    // Intercept Ctrl-C nicely.
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                        _exitEditor = true;
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
                    _canvas.UseColor = !_canvas.UseColor;
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
                case EditorMode.Attackers:
                    ProcessKeyAttackersMode(keyInfo);
                    return;
            }
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
                _defenderLOSTiles = null;
            }

            _statusMsg = $"({_cursorX}, {_cursorY})";
        }

        private void CycleMode()
        {
            var temp = _mode + 1;
            if (!Enum.IsDefined(typeof(EditorMode), temp))
                temp = 0;
            _mode = temp;
            _paintEnabled = false;
        }

        private IList<UnitCharacteristics> LoadUnitsFile()
        {
            var fileContentsAsString = File.ReadAllText(_unitsFileName);
            var unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(fileContentsAsString);
            return unitsList;
        }

        private Terrain GenerateTerrain()
        {
            var mapGenerator = new Generator(_mapGenOptions);
            var terrain = mapGenerator.Create();

            // Invalidate the LoS map.
            _defenderLOSTiles = null;

            return terrain;
        }

        private void LoadOrCreateGeneratorOptions()
        {
            try
            {
                var fileContentsAsString = File.ReadAllText(_optionsFile);
                _mapGenOptions = JsonConvert.DeserializeObject<GeneratorOptions>(fileContentsAsString);
            }
            catch (IOException)
            {
                // TODO: Add logging
            }
            catch (JsonException)
            {
                // TODO: Add logging
            }

            if (_mapGenOptions ==  null)
            {
                _mapGenOptions = new GeneratorOptions()
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
            }
        }

        private void SaveGeneratorOptions()
        {
            if (_mapGenOptions==null)
                return;

            try
            {
                var fileContentsAsString = JsonConvert.SerializeObject(_mapGenOptions);
                File.WriteAllText(_optionsFile, fileContentsAsString);
            }
            catch (IOException ioe)
            {
                // TODO: Add logging
                _statusMsg = ioe.Message;
            }
            catch (JsonException je)
            {
                // TODO: Add logging
                _statusMsg = je.Message;
            }
        }

        private void WriteModeHelp()
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
                case EditorMode.Attackers:
                    WriteModeHelpAttackers(col, ref row);
                    break;
                default:
                    _canvas.WriteText("Not implemented", col, row++, 0);
                    break;
            }

            _canvas.ClearToRight(col, row++);
            _canvas.WriteText("(`) toggle color", col, row++, 0);
            _canvas.WriteText("(L) load scenario", col, row++, 0);
            _canvas.WriteText("(S) save scenario", col, row++, 0);
            _canvas.WriteText("(ESC) exit", col, row++, 0);

            _canvas.ClearToRight(col, row, _scenario.Terrain.Height);
        }

        private void WriteModeHelpTerrain(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Terrain);

            var paintModeIndicator = _paintEnabled? "on" : "off";

            _canvas.ClearToRight(col, row++);
            _canvas.WriteText("(Space) change Tile", col, row++, 0);

            _canvas.ClearToRight(col, row++);
            for (int i=0; i<_scenario.Terrain.TileTypes.Count; ++i)
            {
                var name = _scenario.Terrain.TileTypes[i].Name;
                _canvas.WriteText($"({i+1}) place {name}", col, row++, 0);
            }

            _canvas.ClearToRight(col, row++);
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
                    _scenario.Terrain = GenerateTerrain();
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
                _defenderLOSTiles = null;
            }
        }

        private void WriteModeHelpDefenders(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Defenders);

            _canvas.WriteText($"(T) Team {_teamId}", col, row++, _teamId);

            _canvas.ClearToRight(col, row++);
            _canvas.WriteText("(\\) toggle LoS", col, row++, 0);
            _canvas.WriteText("(Backspace) clear all", col, row++, 0);
            _canvas.WriteText("(1) none", col, row++, 0);

            for (int i=0; i<_defenderClasses.Count; ++i)
                _canvas.WriteText($"({i+2}) {_defenderClasses[i].Name}", col, row++, 0);
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

            _canvas.ClearToRight(col, row++);
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
                _canvas.ClearToRight(col, row++);
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
            }
            else
            {
                _canvas.ClearToRight(col, row++);
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

        private void CycleTileType()
        {
            if (_cursorX<0 || _cursorY<0 || _cursorX>=_scenario.Terrain.Width || _cursorY>=_scenario.Terrain.Height)
                return;

            byte newTileVal = (byte)(_scenario.Terrain.GetTileValue(_cursorX, _cursorY) + 1);
            if (newTileVal >= _scenario.Terrain.TileTypes.Count)
                newTileVal = 0;
            _scenario.Terrain.SetTileValue(_cursorX, _cursorY, newTileVal);

            // Invalidate the LoS map.
            _defenderLOSTiles = null;
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
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt, true);

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
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt, true);

            try
            {
                var filename = String.IsNullOrEmpty(input)? _lastScenarioFilename : input;
                var fileContentsAsString = JsonConvert.SerializeObject(_scenario);
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
        }

        private void DrawDefenderPlacements()
        {
            foreach (var plan in _scenario.DefensePlans)
            {
                // TODO: maybe add vision overlays?
                _canvas.PaintDefensePlan(plan, _scenario.UnitTypes, 0, 0);
            }
        }

        private void RemoveDefender()
        {
            var cursorPos = new Vector2Di(_cursorX, _cursorY);
            foreach (var plan in _scenario.DefensePlans)
                plan.Placements = plan.Placements.Where( (placement) => placement.Position!=cursorPos )
                    .ToList();

            // Invalidate the LOS map.  It will be rebuilt later.
            _defenderLOSTiles = null;
        }

        private void RemoveAllDefenders()
        {
            _scenario.DefensePlans = _scenario.DefensePlans.Where( (plan) => plan.TeamId!=_teamId )
                .ToList();

            // Invalidate the LOS map.  It will be rebuilt later.
            _defenderLOSTiles = null;
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
            _defenderLOSTiles = null;
        }

        private void ClearSpawnsAndGoalsForTeam()
        {
            _scenario.Terrain.SpawnPointsMap.Remove(_teamId);
            _scenario.Terrain.GoalPointsMap.Remove(_teamId);
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
        }

        private void PromptForSpawnDelay()
        {
            const int maxSaneTime = 1000;
            var prompt = $"Spawn Delay Time (enter for {_spawnTime}): ";
            var input = _canvas.PromptForInput(0, _scenario.Terrain.Height, prompt, true);

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
        }

        private void ClearAllAttackersForTeam()
        {
            AttackPlan plan = _scenario.AttackPlans.FirstOrDefault( (ap) => ap.TeamId == _teamId);
            plan?.Spawns.Clear();
        }

        /// <summary>
        /// Builds a grid indicating which tiles are visible from currently placed defenders.
        /// </summary>
        private void BuildDefenderLOSMap()
        {
            _defenderLOSTiles = new int[_scenario.Terrain.Width, _scenario.Terrain.Height];
            foreach (var plan in _scenario.DefensePlans)
            {
                foreach (var defPlacement in plan.Placements)
                {
                    var unitClass = _scenario.UnitTypes.FirstOrDefault( (ut) => ut.Name==defPlacement.UnitType);
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

                                var currentVal = _defenderLOSTiles[x,y];

                                // Using -1 to signify multiple teams have LOS on this spot.
                                if (currentVal==-1)
                                    continue;

                                if (currentVal != plan.TeamId)
                                {
                                    var visible = _scenario.Terrain.HasLineOfSight(defPlacement.Position, pos);
                                    if (visible && currentVal==0)
                                        _defenderLOSTiles[x,y] = plan.TeamId;
                                    else if (visible)
                                        _defenderLOSTiles[x,y] = -1;
                                }
                            }
                        }
                    }
                }

            }
        }

        private void EditPoco<T>(T obj)
        {
            int row = 0;
            _canvas.Init();
            _canvas.BeginFrame();

            _canvas.WriteText($"Edit values for {obj.GetType().Name} -", 0, row++, 0);

            var props = obj.GetType().GetProperties();
            foreach (var prop in props)
            {
                var curVal = Convert.ToString(prop.GetValue(obj));
                var prompt = $"  {prop.Name} (enter for {curVal}):";
                var input = _canvas.PromptForInput(0, row++, prompt, false);

                if (!String.IsNullOrWhiteSpace(input))
                {
                    var newVal = Convert.ChangeType(input, prop.PropertyType);
                    prop.SetValue(obj, newVal);
                }
            }

            _canvas.EndFrame();
            _canvas.Init();
        }

        private void PromptToEditGeneratorOptions()
        {
            EditPoco(_mapGenOptions);
            SaveGeneratorOptions();
        }
    }
}
