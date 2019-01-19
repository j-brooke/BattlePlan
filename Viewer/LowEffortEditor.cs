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
                // Draw the map and everything on it.
                _canvas.BeginFrame();
                WriteModeHelp();
                _canvas.PaintTerrain(_scenario.Terrain, 0, 0);
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

        private const string _unitsFileName = "scenarios/units.json";

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
        private List<UnitCharacteristics> _attackerClasses;
        private List<UnitCharacteristics> _defenderClasses;

        /// <summary>
        /// Called on start, or when a new scenario is loaded.  Rebuilds internal data and such.
        /// </summary>
        private void InitFromScenario()
        {
            if (_scenario.UnitTypes == null)
            {
                var fileContentsAsString = File.ReadAllText(_unitsFileName);
                _scenario.UnitTypes = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(fileContentsAsString);
            }

            // Make lists of which units can attack or defend, for spawn/placement menus.
            _attackerClasses = _scenario.UnitTypes.Where( (uc) => uc.CanAttack ).ToList();
            _defenderClasses = _scenario.UnitTypes.Where( (uc) => uc.CanDefend ).ToList();

            _cursorX = 0;
            _cursorY = 0;
            _mode = EditorMode.Terrain;
            _teamId = _minimumTeamId;
            _paintEnabled = false;
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
            }
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
            var fileContentsAsString = File.ReadAllText("scenarios/units.json");
            var unitsList = JsonConvert.DeserializeObject<List<UnitCharacteristics>>(fileContentsAsString);
            return unitsList;
        }

        private Terrain GenerateTerrain()
        {
            // TODO: add ability to edit generator options in the editor, and save/load
            // as needed.
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

            var mapGenerator = new Generator(_mapGenOptions);
            var terrain = mapGenerator.Create();
            return terrain;
        }

        private void WriteModeHelp()
        {
            int row = 0;
            int col = _scenario.Terrain.Width + 2;
            _canvas.WriteText("(Enter) Mode:", col, row++, 0);
            _canvas.WriteText($"{_mode}", col, row++, 0);

            switch (_mode)
            {
                case EditorMode.Terrain:
                    WriteModeHelpTerrain(col, ref row);
                    break;
                case EditorMode.Defenders:
                    WriteModeHelpDefenders(col, ref row);
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
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=1 && keyNumberValue<=_scenario.Terrain.TileTypes.Count)
            {
                _scenario.Terrain.SetTileValue(_cursorX, _cursorY, (byte)(keyNumberValue-1));
            }
        }

        private void WriteModeHelpDefenders(int col, ref int row)
        {
            Debug.Assert(_mode==EditorMode.Defenders);

            _canvas.WriteText($"(T) Team {_teamId}", col, row++, _teamId);

            _canvas.ClearToRight(col, row++);
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
            }

            int keyNumberValue = keyInfo.KeyChar - '0';
            if (keyNumberValue>=2 && keyNumberValue<=_defenderClasses.Count+1)
            {
                PlaceDefender(_defenderClasses[keyNumberValue-2]);
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
        }

        private void RemoveAllDefenders()
        {
            _scenario.DefensePlans = _scenario.DefensePlans.Where( (plan) => plan.TeamId!=_teamId )
                .ToList();
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
        }
    }
}
