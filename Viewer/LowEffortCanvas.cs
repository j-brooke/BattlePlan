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
        public bool UseDoubleBuffer { get; set; } = true;

        public LowEffortCanvas()
        {
            // Test some of the system rules
            _canSetCursorSize = true;
            try
            {
                Console.CursorSize = 50;
            }
            catch (PlatformNotSupportedException)
            {
                _canSetCursorSize = false;
            }
        }

        public Vector2Di GetDisplaySize()
        {
            return new Vector2Di(Console.BufferWidth, Console.BufferHeight);
        }

        public void Init()
        {
            // Allow us to intercept Ctrl-C, so that we can reset the cursor and colors
            // before exiting.  (Note that we have to turn this off again when reading
            // for a line of input.  See PromptForInput below.)
            Console.TreatControlCAsInput = true;

            _originalCursorSize = Console.CursorSize;
            if (_canSetCursorSize)
                Console.CursorSize = 100;
            _maxRowDrawn = 0;

            Console.Clear();

            if (this.UseDoubleBuffer)
            {
                var height = Console.WindowHeight-1;
                var width = Console.WindowWidth;
                _symbolBackBuffer = new char[height][];
                _fgBackBuffer = new ConsoleColor[height][];
                _bgBackBuffer = new ConsoleColor[height][];

                for (int i=0; i<height; ++i)
                {
                    _symbolBackBuffer[i] = new char[width];
                    _fgBackBuffer[i] = new ConsoleColor[width];
                    _bgBackBuffer[i] = new ConsoleColor[width];
                }
            }

            // Create an array of spaces.  Used for clearing lines.
            var numSpaces = Math.Max(80, Console.BufferWidth);
            _lotsOfSpaces = new char[numSpaces];
            Array.Fill(_lotsOfSpaces, ' ');

            _frameCount = 0;
            _minFrameTime = long.MaxValue;
            _maxFrameTime = 0;
            _totalFrameTime = 0;
        }

        public void ClearScreen()
        {
            Console.ResetColor();
            Console.Clear();
        }

        public void Shutdown()
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, _maxRowDrawn+1);

            _logger.Debug("Frame time (ms) stats: avg={0}; min={1}; max={2}", _totalFrameTime/_frameCount, _minFrameTime, _maxFrameTime);
        }


        public void BeginFrame()
        {
            _frameTimer.Restart();

            // Hide the cursor while drawing the scene, to avoid flicker.
            Console.CursorVisible = false;

            if (_symbolBackBuffer != null)
            {
                // Clear our internal screen buffers.
                for (int i=0; i<_symbolBackBuffer.Length; ++i)
                {
                    Array.Fill(_symbolBackBuffer[i], ' ');
                    Array.Fill(_fgBackBuffer[i], ConsoleColor.White);
                    Array.Fill(_bgBackBuffer[i], ConsoleColor.Black);
                }
            }
        }

        public void EndFrame()
        {
            if (_symbolBackBuffer!=null)
                RenderBackBuffer();

            var frameTime = _frameTimer.ElapsedMilliseconds;
            _frameCount += 1;
            _totalFrameTime += frameTime;
            _minFrameTime = Math.Min(_minFrameTime, frameTime);
            _maxFrameTime = Math.Max(_maxFrameTime, frameTime);

            _logger.Trace("Single frame time: {0}", frameTime);
        }

        public void PaintTerrain(Terrain terrain, int[,] terrainOverride, int canvasOffsetX, int canvasOffsetY)
        {
            for (int row=0; row<terrain.Height; ++row)
            {
                Console.SetCursorPosition(canvasOffsetX, row+canvasOffsetY);

                for (int col=0; col<terrain.Width; ++col)
                {
                    var tileChars = terrain.GetTile(col, row);
                    var fgColor = GetTerrainFGColor(tileChars.Appearance);

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


                    if (_symbolBackBuffer!=null)
                    {
                        var x = col+canvasOffsetX;
                        var y = row+canvasOffsetY;
                        _symbolBackBuffer[y][x] = tileChars.Appearance[0];
                        _fgBackBuffer[y][x] = fgColor;
                        _bgBackBuffer[y][x] = bgColor;
                    }
                    else
                    {
                        if (this.UseColor)
                        {
                            Console.ForegroundColor = fgColor;
                            Console.BackgroundColor = bgColor;
                        }
                        Console.Write(tileChars.Appearance[0]);
                    }
                }
            }

            _maxRowDrawn = Math.Max(_maxRowDrawn, terrain.Height-1 + canvasOffsetY);
        }

        public void PaintTile(int x, int y, char symbol, ConsoleColor fgColor, ConsoleColor bgColor, int canvasOffsetX, int canvasOffsetY)
        {
            if (_symbolBackBuffer!=null)
            {
                var buffX = x + canvasOffsetX;
                var buffY = y + canvasOffsetY;
                _symbolBackBuffer[buffY][buffX] = symbol;
                _fgBackBuffer[buffY][buffX] = fgColor;
                _bgBackBuffer[buffY][buffX] = bgColor;
                return;
            }

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

            if (_symbolBackBuffer!=null)
            {
                row = canvasOffsetY;
                foreach (var evt in textEvents)
                {
                    var col = canvasOffsetX;
                    switch (evt.Type)
                    {
                        case BattleEventType.EndAttack:
                            col = WriteText(evt.SourceEntity, col, row, GetTeamColor(evt.SourceTeamId));
                            col = WriteText(" damages ", col, row, GetTextColor());
                            col = WriteText(evt.TargetEntity, col, row, GetTeamColor(evt.TargetTeamId));
                            col = WriteText(" for ", col, row, GetTextColor());
                            col = WriteText(evt.DamageAmount.ToString(), col, row, GetDamageColor());
                            break;
                        case BattleEventType.ReachesGoal:
                            col = WriteText(evt.SourceEntity, col, row, GetTeamColor(evt.SourceTeamId));
                            col = WriteText(" reaches goal!", col, row, GetTextColor());
                            break;
                        case BattleEventType.Die:
                            col = WriteText(evt.SourceEntity, col, row, GetTeamColor(evt.SourceTeamId));
                            col = WriteText(" dies!", col, row, GetTextColor());
                            break;
                    }

                    row += 1;
                }
                return;
            }

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
            var color = GetTeamColor(teamIdColor);
            WriteText(text, x, y, color);
        }

        public void ClearToRight(int x, int y)
        {
            ClearToRight(x, y, y);
        }

        public void ClearToRight(int x, int fromY, int toY)
        {
            if (_symbolBackBuffer!=null)
                return;

            for (var y=fromY; y<=toY; ++y)
            {
                Console.SetCursorPosition(x, y);
                ClearToEndOfLine();
            }
        }

        public string PromptForInput(int x, int y, string prompt, bool clearLineAfter)
        {
            _logger.Trace("Prompting for input: {0}", prompt);

            // On Windows, if this is true, it messes up input from Console.ReadLine.  One effect is that
            // things like backspace show up as control characters in the returned string, instead of
            // removing the last character as you would expect.  It also sometimes requires an extra CR
            // and then gets out of sync.
            Console.TreatControlCAsInput = false;

            Console.ResetColor();
            Console.CursorVisible = true;
            if (_canSetCursorSize)
                Console.CursorSize = _originalCursorSize;

            // Make sure the line we want to prompt on is clean.
            Console.SetCursorPosition(x, y);
            ClearToEndOfLine();

            Console.SetCursorPosition(x, y);
            Console.Write(prompt);

            var input = Console.ReadLine();

            if (_logger.IsTraceEnabled)
            {
                _logger.Trace("Input received: {0}", input);
                var buff = new System.Text.StringBuilder();
                foreach (var ch in input)
                    buff.AppendFormat("{0:x2} ", (int)ch);
                _logger.Trace("Input received hex: {0}", buff.ToString());
            }

            if (clearLineAfter)
            {
                Console.SetCursorPosition(x, y);
                ClearToEndOfLine();
            }

            if (_canSetCursorSize)
                Console.CursorSize = 100;
            Console.TreatControlCAsInput = true;

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

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly System.Diagnostics.Stopwatch _frameTimer = new System.Diagnostics.Stopwatch();
        private long _frameCount;
        private long _totalFrameTime;
        private long _minFrameTime;
        private long _maxFrameTime;

        // Used to track where to put the cursor when we shut down.
        private int _maxRowDrawn = 0;

        // An array of just spaces, used for clearing lines.
        private char[] _lotsOfSpaces;

        private int _originalCursorSize = 100;

        private char[][] _symbolBackBuffer;
        private ConsoleColor[][] _fgBackBuffer;
        private ConsoleColor[][] _bgBackBuffer;
        private bool _canSetCursorSize;

        private void WriteText(string text, ConsoleColor color)
        {
            if (this.UseColor)
                Console.ForegroundColor = color;
            Console.Write(text);
        }

        private void ClearToEndOfLine()
        {
            if (_symbolBackBuffer!=null)
                return;

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

        private void RenderBackBuffer()
        {
            Debug.Assert(_symbolBackBuffer!=null && _fgBackBuffer!=null && _bgBackBuffer!=null);

            var height = Math.Min(Console.BufferHeight, _symbolBackBuffer.Length);
            var width = Math.Min(Console.BufferWidth, _symbolBackBuffer[0].Length);
            if (height<1 || width<1)
                return;

            for (int y=0; y<height; ++y)
            {
                Console.SetCursorPosition(0, y);
                var symRow = _symbolBackBuffer[y];
                var fgRow = _fgBackBuffer[y];
                var bgRow = _bgBackBuffer[y];

                int x=0;
                while (x<width)
                {
                    // Figure out how many characters in a row share the same colors.
                    var span = 1;
                    while (x+span<width)
                    {
                        if ( !this.UseColor || (fgRow[x]==fgRow[x+span] && bgRow[x]==bgRow[x+span]) )
                            span += 1;
                        else
                            break;
                    }
                    if (this.UseColor)
                    {
                        Console.ForegroundColor = fgRow[x];
                        Console.BackgroundColor = bgRow[x];
                    }
                    Console.Write(symRow, x, span);
                    x += span;
                }
            }
        }

        private int WriteText(string text, int x, int y, ConsoleColor textColor)
        {
            if (_symbolBackBuffer!=null)
            {
                var symRow = _symbolBackBuffer[y];
                var fgRow = _fgBackBuffer[y];
                int i;
                for (i=0; i<text.Length && x+i<symRow.Length; ++i)
                {
                    symRow[x+i] = text[i];
                    fgRow[x+i] = textColor;
                }

                return x+i;
            }

            Console.ResetColor();
            Console.SetCursorPosition(x, y);
            WriteText(text, textColor);
            ClearToEndOfLine();
            _maxRowDrawn = Math.Max(_maxRowDrawn, y);

            return x+text.Length;
        }

    }
}