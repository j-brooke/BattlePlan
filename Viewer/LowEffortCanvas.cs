using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using BattlePlan.Common;

namespace BattlePlan.Viewer
{
    /// <summary>
    /// Tool for drawing the game-specific UI to a terminal window.
    /// </summary>
    internal class LowEffortCanvas
    {
        public bool UseColor { get; set; } = true;


        public Vector2Di GetDisplaySize()
        {
            return new Vector2Di(Console.BufferWidth, Console.BufferHeight);
        }

        public void Init()
        {
            Console.TreatControlCAsInput = true;
            Console.Clear();
            _maxRowDrawn = 0;

            // Create an array of spaces.  Used for clearing lines.
            var numSpaces = Math.Max(80, Console.BufferWidth);
            _lotsOfSpaces = new char[numSpaces];
            Array.Fill(_lotsOfSpaces, ' ');
        }

        public void Shutdown()
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, _maxRowDrawn+1);
        }


        public void BeginFrame()
        {
            // Hide the cursor while drawing the scene, to avoid flicker.
            Console.CursorVisible = false;
        }

        public void EndFrame()
        {
        }

        public void PaintTerrain(Terrain terrain, int[,] terrainOverride, int canvasOffsetX, int canvasOffsetY)
        {
            for (int row=0; row<terrain.Height; ++row)
            {
                Console.SetCursorPosition(canvasOffsetX, row+canvasOffsetY);

                for (int col=0; col<terrain.Width; ++col)
                {
                    var tileChars = terrain.GetTile(col, row);
                    if (this.UseColor)
                    {
                        Console.ForegroundColor = GetTerrainFGColor(tileChars.Appearance);

                        var overrideTeam = (terrainOverride!=null)? terrainOverride[col,row] : 0;

                        // Normally the background color should be the terrain BG color.  But if there's an override here,
                        // it's either a team color (>=1) or -1 to indicate multiple teams.
                        ConsoleColor bgColor;
                        if (overrideTeam==0)
                            bgColor = GetTerrainBGColor(tileChars.Appearance);
                        else if (overrideTeam==-1)
                            bgColor = GetDamageColor();
                        else
                            bgColor = GetTeamColor(overrideTeam);
                        Console.BackgroundColor = bgColor;
                    }
                    Console.Write(tileChars.Appearance[0]);
                }
            }

            _maxRowDrawn = Math.Max(_maxRowDrawn, terrain.Height-1 + canvasOffsetY);
        }

        public void PaintTile(int x, int y, char symbol, ConsoleColor fgColor, ConsoleColor bgColor, int canvasOffsetX, int canvasOffsetY)
        {
            Console.SetCursorPosition(x+canvasOffsetX, y+canvasOffsetY);

            if (this.UseColor)
            {
                Console.ForegroundColor = fgColor;
                Console.BackgroundColor = bgColor;
            }
            Console.Write(symbol);
            _maxRowDrawn = Math.Max(_maxRowDrawn, y + canvasOffsetY);
        }

        public void PaintEntities(IEnumerable<ViewEntity> entities, int canvasOffsetX, int canvasOffsetY)
        {
            foreach(var entity in entities)
            {
                PaintTile(
                    entity.Position.X,
                    entity.Position.Y,
                    entity.Symbol,
                    GetTeamColor(entity.TeamId),
                    GetTerrainBGColor(" "),
                    canvasOffsetX,
                    canvasOffsetY);
            }
        }

        public void PaintSpawnPoints(Terrain terrain, int canvasOffsetX, int canvasOffsetY)
        {
            const char spawnPointSymbol = 'O';
            var bgColor = GetTerrainBGColor(" ");
            if (terrain?.SpawnPointsMap != null)
            {
                foreach (var keyValuePair in terrain.SpawnPointsMap)
                {
                    var teamId = keyValuePair.Key;
                    var spawnPointsForTeam = keyValuePair.Value;
                    var teamColor = GetTeamColor(teamId);
                    foreach (var spawnPoint in spawnPointsForTeam)
                    {
                        PaintTile(
                            spawnPoint.X,
                            spawnPoint.Y,
                            spawnPointSymbol,
                            teamColor,
                            bgColor,
                            canvasOffsetX,
                            canvasOffsetY);
                    }
                }
            }
        }

        public void PaintDamageIndicators(IEnumerable<BattleEvent> dmgEvents, int canvasOffsetX, int canvasOffsetY)
        {
            const char dmgSymbol = '*';
            foreach(var evt in dmgEvents)
            {
                Debug.Assert(evt.TargetLocation.HasValue);
                PaintTile(
                    evt.TargetLocation.Value.X,
                    evt.TargetLocation.Value.Y,
                    dmgSymbol,
                    GetDamageColor(),
                    GetTerrainBGColor(" "),
                    canvasOffsetX,
                    canvasOffsetY);
            }
        }

        public void PaintGoalPoints(Terrain terrain, int canvasOffsetX, int canvasOffsetY)
        {
            const char goalPointSymbol = 'X';
            var bgColor = GetTerrainBGColor(" ");
            if (terrain?.SpawnPointsMap != null)
            {
                foreach (var keyValuePair in terrain.GoalPointsMap)
                {
                    var teamId = keyValuePair.Key;
                    var goalPointsForTeam = keyValuePair.Value;
                    var teamColor = GetTeamColor(teamId);
                    foreach (var goalPoint in goalPointsForTeam)
                    {
                        PaintTile(
                            goalPoint.X,
                            goalPoint.Y,
                            goalPointSymbol,
                            teamColor,
                            bgColor,
                            canvasOffsetX,
                            canvasOffsetY);
                    }
                }
            }
        }

        public void WriteTextEvents(IEnumerable<BattleEvent> textEvents, int canvasOffsetX, int canvasOffsetY)
        {
            int row = 0;

            Console.ResetColor();
            foreach (var evt in textEvents)
            {
                Console.SetCursorPosition(canvasOffsetX, canvasOffsetY + row);

                switch (evt.Type)
                {
                    case BattleEventType.EndAttack:
                        WriteText(evt.SourceEntity, GetTeamColor(evt.SourceTeamId));
                        WriteText(" damages ", GetTextColor());
                        WriteText(evt.TargetEntity, GetTeamColor(evt.TargetTeamId));
                        WriteText(" for ", GetTextColor());
                        WriteText(evt.DamageAmount.ToString(), GetDamageColor());
                        ClearToEndOfLine();
                        break;
                    case BattleEventType.ReachesGoal:
                        WriteText(evt.SourceEntity, GetTeamColor(evt.SourceTeamId));
                        WriteText(" reaches goal!", GetTextColor());
                        ClearToEndOfLine();
                        break;
                    case BattleEventType.Die:
                        WriteText(evt.SourceEntity, GetTeamColor(evt.SourceTeamId));
                        WriteText(" dies!", GetTextColor());
                        ClearToEndOfLine();
                        break;
                }

                row += 1;
            }
            _maxRowDrawn = Math.Max(_maxRowDrawn, row + canvasOffsetY - 1);
        }

        public void ShowCursor(int x, int y)
        {
            Console.SetCursorPosition(x, y);
            Console.CursorVisible = true;
        }

        public ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey(true);
        }

        public ConsoleKeyInfo? ReadKeyWithoutBlocking()
        {
            if (Console.KeyAvailable)
                return Console.ReadKey(true);
            else
                return null;
        }

        public void WriteText(string text, int x, int y, int teamIdColor)
        {
            Console.ResetColor();
            Console.SetCursorPosition(x, y);
            var color = GetTeamColor(teamIdColor);
            WriteText(text, color);
            ClearToEndOfLine();
        }

        public void ClearToRight(int x, int y)
        {
            ClearToRight(x, y, y);
        }

        public void ClearToRight(int x, int fromY, int toY)
        {
            for (var y=fromY; y<=toY; ++y)
            {
                Console.SetCursorPosition(x, y);
                ClearToEndOfLine();
            }
        }

        public string PromptForInput(int x, int y, string prompt, bool clearLineAfter)
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.SetCursorPosition(x, y);
            ClearToEndOfLine();

            Console.SetCursorPosition(x, y);
            Console.Write(prompt);
            var input = Console.ReadLine();

            if (clearLineAfter)
            {
                Console.SetCursorPosition(x, y);
                ClearToEndOfLine();
            }

            return input;
        }

        public void PaintDefensePlan(DefensePlan plan, IList<UnitCharacteristics> unitChars, int canvasOffsetX, int canvasOffsetY)
        {
            var teamColor = GetTeamColor(plan.TeamId);
            var bgColor = GetTerrainBGColor(" ");
            foreach (var placement in plan.Placements)
            {
                var unitClass = unitChars.FirstOrDefault( (cls) => cls.Name==placement.UnitType );
                PaintTile(
                    placement.Position.X,
                    placement.Position.Y,
                    (unitClass!=null)? unitClass.Symbol : '?',
                    teamColor,
                    bgColor,
                    canvasOffsetX,
                    canvasOffsetY);
            }
        }

        // Used to track where to put the cursor when we shut down.
        private int _maxRowDrawn = 0;

        // An array of just spaces, used for clearing lines.
        private char[] _lotsOfSpaces;

        private void WriteText(string text, ConsoleColor color)
        {
            if (this.UseColor)
                Console.ForegroundColor = color;
            Console.Write(text);
        }

        private void ClearToEndOfLine()
        {
            // Windows doesn't recognize ANSI command sequences like the one to clear to the end of the line, so
            // for it we have to just write a lot of spaces.  Using spaces on Mac causes horrible flickering,
            // which is otherwise absent for me.  Maybe the use of ANSI commands changes its buffering strategy
            // or something.  Windows flickers horribly no matter what and will require refactoring to fix.
            if (Environment.OSVersion.Platform==PlatformID.Win32NT)
            {
                var spaceCount = Console.BufferWidth - Console.CursorLeft;
                Console.Write(_lotsOfSpaces, 0, spaceCount);
            }
            else
            {
                Console.Write("\u001B[K");
            }
        }

        // TODO: Color values should probably come from a config file.
        private ConsoleColor GetTerrainFGColor(string tileSymbol)
        {
            switch (tileSymbol)
            {
                case ":": return ConsoleColor.White;
                case " ": return ConsoleColor.DarkGray;
                case "~": return ConsoleColor.Blue;
                case "@": return ConsoleColor.White;
                default: return ConsoleColor.Black;
            }
        }
        private ConsoleColor GetTerrainBGColor(string tileSymbol)
        {
            switch (tileSymbol)
            {
                case ":": return ConsoleColor.Gray;
                case " ": return ConsoleColor.DarkGray;
                case "~": return ConsoleColor.DarkBlue;
                case "@": return ConsoleColor.DarkGray;
                default: return ConsoleColor.White;
            }
        }

        private ConsoleColor GetTeamColor(int teamId)
        {
            switch (teamId)
            {
                case 1: return ConsoleColor.Yellow;
                case 2: return ConsoleColor.Green;
                default: return ConsoleColor.White;
            }
        }

        private ConsoleColor GetDamageColor()
        {
            return ConsoleColor.Red;
        }

        private ConsoleColor GetTextColor()
        {
            return ConsoleColor.White;
        }
    }
}