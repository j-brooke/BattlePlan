using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BattlePlan.Common;

namespace BattlePlan.Viewer
{
    public class LowEffortViewer
    {
        public double FrameTimeSeconds { get; set; } = 0.2;

        public double DamageDisplayTimeSeconds { get; set; } = 0.2;

        public bool UseColor { get; set; } = true;

        public void ShowBattleResolution(BattleResolution resolution)
        {
            // Clear screen
            Console.Clear();
            Console.CursorVisible = false;

            _entities = new Dictionary<string, ViewEntity>();
            _rencentDamageEvents = new Queue<BattleEvent>();
            _recentTextEvents = new Queue<BattleEvent>();

            _unitTypeMap = new Dictionary<string, UnitCharacteristics>();
            if (resolution.UnitTypes != null)
            {
                foreach (var unitType in resolution.UnitTypes)
                    _unitTypeMap.Add(unitType.Name, unitType);
            }

            var maxTextEvents = resolution.Terrain.Height;

            var eventQueue = new Queue<BattleEvent>(resolution.Events.OrderBy( (evt) => evt.Time ) );
            double time = 0.0;
            bool firstPass = true;
            var frameTimer = new System.Diagnostics.Stopwatch();
            while (eventQueue.Count>0 || firstPass)
            {
                // Reset the stopwatch so we know how long we're taking processing all this.
                frameTimer.Restart();

                // Process all remaining events before our time cutoff.
                bool moreEventsThisFrame = true;
                while (moreEventsThisFrame && eventQueue.Count>0)
                {
                    var nextEvt = eventQueue.Peek();
                    if (nextEvt.Time<= time)
                    {
                        eventQueue.Dequeue();
                        ProcessEvent(nextEvt);
                    }
                    else
                    {
                        moreEventsThisFrame = false;
                    }
                }

                RemoveOldDamageEvents(time);
                RemoveOldTextEvents(maxTextEvents);

                // Draw text events first.  Otherwise if they go past the edge of the screen, they trash part of the map.
                WriteTextEvents(_recentTextEvents, resolution.Terrain.Width + 2, 0);

                // Draw the map and everything on it.
                PaintTerrain(resolution.Terrain, 0, 0);
                PaintSpawnPoints(resolution.Terrain, 0, 0);
                PaintGoalPoints(resolution.Terrain, 0, 0);
                PaintEntities(_entities.Values, 0, 0);
                PaintDamageIndicators(_rencentDamageEvents, 0, 0);

                firstPass = false;
                time += this.FrameTimeSeconds;

                // Reset things while we wait, in case of ctrl-c.
                Console.SetCursorPosition(0, resolution.Terrain.Height);
                Console.ResetColor();

                var processFrameTimeMS = frameTimer.ElapsedMilliseconds;
                var sleepTimeMS = Math.Max(0, (int)(this.FrameTimeSeconds*1000 - processFrameTimeMS));
                System.Threading.Thread.Sleep(sleepTimeMS);
            }
        }

        private Dictionary<string,ViewEntity> _entities;
        private Queue<BattleEvent> _rencentDamageEvents;
        private Queue<BattleEvent> _recentTextEvents;
        private Dictionary<string,UnitCharacteristics> _unitTypeMap;

        private void ProcessEvent(BattleEvent evt)
        {
            switch (evt.Type)
            {
                case BattleEventType.Spawn:
                    ProcessSpawnEvent(evt);
                    break;
                case BattleEventType.Despawn:
                    ProcessDespawnEvent(evt);
                    break;
                case BattleEventType.EndMovement:
                    ProcessMoveEvent(evt);
                    break;
                case BattleEventType.EndAttack:
                    ProcessDamageEvent(evt);
                    break;
                case BattleEventType.ReachesGoal:
                    ProcessReachesGoalEvent(evt);
                    break;
                case BattleEventType.Die:
                    ProcessDiesEvent(evt);
                    break;
            }
        }

        private void ProcessSpawnEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.Spawn);

            var newEnt = new ViewEntity()
            {
                Id = evt.SourceEntity,
                TeamId = evt.SourceTeamId,
                UnitType = _unitTypeMap[evt.SourceClass],
                Position = evt.SourceLocation.Value,
            };
            newEnt.Symbol = newEnt.UnitType.Symbol;

            _entities[newEnt.Id] = newEnt;
        }

        private void ProcessDespawnEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.Despawn);
            _entities.Remove(evt.SourceEntity);
        }

        private void ProcessMoveEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.EndMovement);
            var entity = _entities[evt.SourceEntity];
            entity.Position = evt.TargetLocation.Value;
        }

        private void ProcessDamageEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.EndAttack);
            if (evt.TargetEntity != null)
            {
                _rencentDamageEvents.Enqueue(evt);
                _recentTextEvents.Enqueue(evt);
            }
        }

        private void ProcessReachesGoalEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.ReachesGoal);
            _recentTextEvents.Enqueue(evt);
        }

        private void ProcessDiesEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.Die);
            _recentTextEvents.Enqueue(evt);
        }

        private void RemoveOldDamageEvents(double currentTime)
        {
            var thresholdTime = currentTime - this.DamageDisplayTimeSeconds;

            bool keepLooking = true;
            while (_rencentDamageEvents.Count>0 && keepLooking)
            {
                var evt = _rencentDamageEvents.Peek();
                if (evt.Time<=thresholdTime)
                    _rencentDamageEvents.Dequeue();
                else
                    keepLooking = false;
            }
        }

        private void RemoveOldTextEvents(int maxNumberOfTextEvents)
        {
            while (_recentTextEvents.Count>maxNumberOfTextEvents)
                _recentTextEvents.Dequeue();
        }

        private void PaintTerrain(Terrain terrain, int canvasOffsetX, int canvasOffsetY)
        {
            // Assume the screen is cleared already as needed.
            for (int row=0; row<terrain.Height; ++row)
            {
                Console.SetCursorPosition(canvasOffsetX, row+canvasOffsetY);

                for (int col=0; col<terrain.Width; ++col)
                {
                    var tileChars = terrain.GetTile(col, row);
                    if (this.UseColor)
                    {
                        Console.ForegroundColor = GetTerrainFGColor(tileChars.Appearance);
                        Console.BackgroundColor = GetTerrainBGColor(tileChars.Appearance);
                    }
                    Console.Write(tileChars.Appearance[0]);
                }
            }
        }

        private void PaintTile(int x, int y, char symbol, ConsoleColor fgColor, ConsoleColor bgColor, int canvasOffsetX, int canvasOffsetY)
        {
            Console.SetCursorPosition(x+canvasOffsetX, y+canvasOffsetY);

            if (this.UseColor)
            {
                Console.ForegroundColor = fgColor;
                Console.BackgroundColor = bgColor;
            }
            Console.Write(symbol);
        }

        private void PaintEntities(IEnumerable<ViewEntity> entities, int canvasOffsetX, int canvasOffsetY)
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

        private void PaintSpawnPoints(Terrain terrain, int canvasOffsetX, int canvasOffsetY)
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

        private void PaintDamageIndicators(IEnumerable<BattleEvent> dmgEvents, int canvasOffsetX, int canvasOffsetY)
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

        private void PaintGoalPoints(Terrain terrain, int canvasOffsetX, int canvasOffsetY)
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

        private void WriteTextEvents(IEnumerable<BattleEvent> textEvents, int canvasOffsetX, int canvasOffsetY)
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
        }

        private void WriteText(string text, ConsoleColor color)
        {
            if (this.UseColor)
                Console.ForegroundColor = color;
            Console.Write(text);
        }

        private void ClearToEndOfLine()
        {
            Console.Write("\u001B[K");
        }

        // TODO: Color values should probably come from a config file.
        private ConsoleColor GetTerrainFGColor(string tileSymbol)
        {
            switch (tileSymbol)
            {
                case ":": return ConsoleColor.White;
                case " ": return ConsoleColor.DarkGray;
                default: return ConsoleColor.Black;
            }
        }
        private ConsoleColor GetTerrainBGColor(string tileSymbol)
        {
            switch (tileSymbol)
            {
                case ":": return ConsoleColor.Gray;
                case " ": return ConsoleColor.DarkGray;
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
