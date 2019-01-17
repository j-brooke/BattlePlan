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
        public void EditScenario(Scenario foo)
        {
            _scenario = foo;
            if (_scenario == null)
            {
                _scenario = new Scenario()
                {
                    UnitTypes = LoadUnitsFile(),
                };
            }
            if (_scenario.Terrain == null)
                _scenario.Terrain = GenerateTerrain();

            // TODO: create _entities from scenario attack/defense plans.

            _canvas.Init();
            _cursorX = 0;
            _cursorY = 0;
            _mode = EditorMode.Terrain;

            _exitEditor = false;
            while (!_exitEditor)
            {
                // Draw the map and everything on it.
                _canvas.BeginFrame();
                WriteModeHelp();
                _canvas.PaintTerrain(_scenario.Terrain, 0, 0);
                _canvas.PaintSpawnPoints(_scenario.Terrain, 0, 0);
                _canvas.PaintGoalPoints(_scenario.Terrain, 0, 0);
                _canvas.EndFrame();

                // TODO: write menus and stuff

                _canvas.ShowCursor(_cursorX, _cursorY);
                ProcessUserInput();
            }

            _canvas.Shutdown();
        }

        private readonly LowEffortCanvas _canvas = new LowEffortCanvas();
        private GeneratorOptions _mapGenOptions;
        private Scenario _scenario;
        private int _cursorX;
        private int _cursorY;
        private bool _exitEditor;
        private EditorMode _mode;
        private bool _paintEnabled;

        private void ProcessUserInput()
        {
            // Wait for a key press.
            var keyInfo = _canvas.ReadKey();

            switch (keyInfo.Key)
            {
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    MoveCursor(0, -1);
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    MoveCursor(0, +1);
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.H:
                    MoveCursor(-1, 0);
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.L:
                    MoveCursor(+1, 0);
                    break;
                case ConsoleKey.Escape:
                    _exitEditor = true;
                    break;
                case ConsoleKey.C:
                    if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
                        _exitEditor = true;
                    break;
                case ConsoleKey.Enter:
                    CycleMode();
                    break;
                default:
                    switch (_mode)
                    {
                        case EditorMode.Terrain:
                            ProcessKeyTerrainMode(keyInfo);
                            break;
                    }
                    break;
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
                default:
                    _canvas.WriteText("Not implemented", col, row++, 0);
                    break;
            }

            _canvas.ClearToRight(col, row++);
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
    }
}