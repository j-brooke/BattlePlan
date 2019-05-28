using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using BattlePlanEngine.Model;

namespace BattlePlanConsole.Viewer
{
    /// <summary>
    /// Class that shows battle results by animating ASCII symbols on a terminal window.
    /// </summary>
    public class LowEffortViewer
    {
        public bool UseColor { get; set; } = true;
        public double MaxFps { get; set; } = 10;
        public double DamageDisplayTimeSeconds { get; set; } = 0.2;

        /// <summary>
        /// Amount of game-time to advance for each "step" (spacebar)
        /// </summary>
        public double StepTimeSeconds { get; set; } = 0.25;

        /// <summary>
        /// Amount of game-time to add/subract for "skips" (L/R arrow keys)
        /// </summary>
        public double SkipTimeSeconds { get; set; } = 10.0;

        /// <summary>
        /// Shows a battle result by animating ASCII symbols on a terminal window.
        /// </summary>
        public void ShowBattleResolution(BattleResolution resolution)
        {
            _resolution = resolution;

            // If the resolution had error messages, show those and exit.
            if (_resolution.ErrorMessages!=null && _resolution.ErrorMessages.Count>0)
            {
                ShowErrorErrorMessages(resolution);
                return;
            }

            _topMapRow = resolution.BannerText.Count;
            _statusBarRow = _topMapRow + resolution.Terrain.Height;

            _canvas.Init(_statusBarRow+1);
            _canvas.UseColor = UseColor;

            _unitTypeMap = new Dictionary<string, UnitCharacteristics>();
            if (resolution.UnitTypes != null)
            {
                foreach (var unitType in resolution.UnitTypes)
                    _unitTypeMap.Add(unitType.Name, unitType);
            }

            var maxTextEvents = resolution.Terrain.Height;
            var minFrameTimeMS = (int)(1000 / this.MaxFps);

            ResetDisplayTime(0.0);

            // _displaySpeed is how fast the game results are shown, as a multiple of real elapsed time.
            _displaySpeed = 1.0;
            _exitRequested = false;

            var frameTimer = new System.Diagnostics.Stopwatch();
            long lastFrameTimeMS = 0;
            bool firstPass = true;
            while ((_eventQueue.Count>0 || firstPass) && !_exitRequested)
            {
                // Reset the stopwatch so we know how long we're taking processing all this.
                frameTimer.Restart();

                if (_repaintAll)
                {
                    _canvas.ClearScreen();
                    _repaintAll = false;
                }

                _displayTime += _displaySpeed * lastFrameTimeMS / 1000;

                // Process all remaining events before our time cutoff.
                bool moreEventsThisFrame = true;
                while (moreEventsThisFrame && _eventQueue.Count>0)
                {
                    var nextEvt = _eventQueue.Peek();
                    if (nextEvt.Time<= _displayTime)
                    {
                        _eventQueue.Dequeue();
                        ProcessEvent(nextEvt);
                    }
                    else
                    {
                        moreEventsThisFrame = false;
                    }
                }

                // Damage markers (red *'s) should only exist briefly.
                RemoveOldDamageEvents(_displayTime);

                // There's a limit to how many text events (e.g., "Archer3 damages Grunt17 for 20") on screen.
                RemoveOldTextEvents(maxTextEvents);

                _canvas.BeginFrame();
                WriteBannerText();

                _canvas.WriteTextEvents(_recentTextEvents, resolution.Terrain.Width + 1, _topMapRow);

                // Draw the map and everything on it.
                _canvas.PaintTerrain(resolution.Terrain, null, 0, _topMapRow);
                _canvas.PaintSpawnPoints(resolution.Terrain, 0, _topMapRow);
                _canvas.PaintGoalPoints(resolution.Terrain, 0, _topMapRow);
                _canvas.PaintEntities(_entities.Values, 0, _topMapRow);
                _canvas.PaintDamageIndicators(_rencentDamageEvents, 0, _topMapRow);

                WriteKeyHelp();

                _canvas.EndFrame();

                // Check for key presses without blocking.
                ProcessUserInput();

                firstPass = false;

                // Figure out how long to sleep before the next frame (if at all).
                var processFrameTimeMS = frameTimer.ElapsedMilliseconds;
                var sleepTimeMS = Math.Max(0, (int)(minFrameTimeMS - processFrameTimeMS));
                System.Threading.Thread.Sleep(sleepTimeMS);

                lastFrameTimeMS = frameTimer.ElapsedMilliseconds;
            }

            DrawScoreFrame();

            _canvas.Shutdown();
        }

        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly LowEffortCanvas _canvas = new LowEffortCanvas();
        private BattleResolution _resolution;
        private Queue<BattleEvent> _eventQueue;
        private Dictionary<string,ViewEntity> _entities;
        private Queue<BattleEvent> _rencentDamageEvents;
        private Queue<BattleEvent> _recentTextEvents;
        private Dictionary<string,UnitCharacteristics> _unitTypeMap;
        private bool _exitRequested = false;
        private double _displayTime;
        private double _displaySpeed;
        private bool _repaintAll;

        private int _topMapRow;
        private int _statusBarRow;

        /// <summary>
        /// Update our sprites and things based on a BattleResolution's BattleEvent.
        /// </summary>
        private void ProcessEvent(BattleEvent evt)
        {
            // Note that some BattleEventTypes aren't relevant to us.  For example: BeginMovement.
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

        /// <summary>
        /// Look for key presses and handle them (but don't block)
        /// </summary>
        private void ProcessUserInput()
        {
            var keyInfo = _canvas.ReadKeyWithoutBlocking();
            if (keyInfo.HasValue)
            {
                // The ConsoleKeyInfo.Key property mostly has special stuff like ESC, Ctrl, arrows, etc.  It doesn't
                // have all of the normal stuff like "1".
                switch (keyInfo.Value.Key)
                {
                    // Exit the app
                    case ConsoleKey.C:
                        if ((keyInfo.Value.Modifiers & ConsoleModifiers.Control)!=0)
                            _exitRequested = true;
                        return;
                    case ConsoleKey.Escape:
                        _exitRequested = true;
                        return;

                    // Rewind by a few seconds.  We basically have to reset everything and then process to
                    // the new desired time all at once.
                    case ConsoleKey.LeftArrow:
                        ResetDisplayTime(_displayTime - this.SkipTimeSeconds);
                        return;

                    // Skip forward by a few seconds.  Just set the time - the main loop will handle the rest.
                    case ConsoleKey.RightArrow:
                        _displayTime += this.SkipTimeSeconds;
                        return;

                    // Step forward by a small amount and pause playback.
                    case ConsoleKey.Spacebar:
                        _displayTime += this.StepTimeSeconds;
                        _displaySpeed = 0.0;
                        return;
                }

                // We can look in KeyChar for regular symbols.  This gives us the versions modified by shift, ctrl, etc.,
                // so for instance, if we wanted to use this to look for Ctrl-C, it would give us 0x03, not 'c'.
                switch (keyInfo.Value.KeyChar)
                {
                    // Play speed controls
                    case '1':
                        _displaySpeed = 1.0;
                        return;
                    case '2':
                        _displaySpeed = 2.0;
                        return;
                    case '3':
                        _displaySpeed = 4.0;
                        return;
                    case '4':
                        _displaySpeed = 8.0;
                        return;
                    case '5':
                        _displaySpeed = 16.0;
                        return;
                }
            }
        }

        /// <summary>
        /// Reset all of our display entities.  They will be rebuilt up to the desired time in the main loop.
        /// </summary>
        private void ResetDisplayTime(double setToTime)
        {
            _eventQueue = new Queue<BattleEvent>(_resolution.Events.OrderBy( (evt) => evt.Time ) );
            _entities = new Dictionary<string, ViewEntity>();
            _rencentDamageEvents = new Queue<BattleEvent>();
            _recentTextEvents = new Queue<BattleEvent>();
            _displayTime = Math.Max(0, setToTime);
            _repaintAll = true;
        }

        private void WriteKeyHelp()
        {
            var msg = $"{_displayTime.ToString("F2")}  (1-5) speed, (Space) pause/step, (L/R-arrow) skip, (ESC) exit";
            _canvas.WriteText(msg, 0, _statusBarRow, LowEffortCanvas.RegularTextColor);
        }

        /// <summary>
        /// Clear the screen and fill it with error text.
        /// </summary>
        private void ShowErrorErrorMessages(BattleResolution resolution)
        {
            int row = 0;

            _canvas.ClearScreen();
            _canvas.WriteTextDirect("Scenario is invalid", 0, row++);

            foreach (var err in resolution.ErrorMessages)
            {
                _canvas.WriteTextDirect("  * " + err, 0, row++);
            }

            row += 1;
            _canvas.WriteTextDirect("Press a key to continue", 0, row++);
            _canvas.ReadKey();
        }

        /// <summary>
        /// Draw the final position of entities, and text saying who won.
        /// </summary>
        private void DrawScoreFrame()
        {
            _canvas.BeginFrame();

            int sidebarCol = _resolution.Terrain.Width + 1;
            int row = _topMapRow;
            var totalBreachCount = _resolution.AttackerBreachCounts.Values.Sum();
            var totalDefenderCasualties = _resolution.DefenderCasualtyCounts.Values.Sum();

            if (totalBreachCount==0)
                _canvas.WriteText("Victory", sidebarCol, row++, 2);
            else
                _canvas.WriteText("Failure", sidebarCol, row++, -1);

            _canvas.WriteText($"{totalBreachCount} attacker breaches", sidebarCol, row++, LowEffortCanvas.RegularTextColor);
            _canvas.WriteText($"{totalDefenderCasualties} defender casualties", sidebarCol, row++, LowEffortCanvas.RegularTextColor);

            if (_resolution.ChallengesAchieved!=null && _resolution.ChallengesAchieved.Count>0)
            {
                row += 1;
                _canvas.WriteText("Challenges achieved", sidebarCol, row++, LowEffortCanvas.RegularTextColor);
                foreach (var challenge in _resolution.ChallengesAchieved)
                    _canvas.WriteText($"  {challenge.Name}", sidebarCol, row++, challenge.PlayerTeamId);

            }

            if (_resolution.ChallengesFailed!=null && _resolution.ChallengesFailed.Count>0)
            {
                row += 1;
                _canvas.WriteText("Challenges failed", sidebarCol, row++, LowEffortCanvas.RegularTextColor);
                foreach (var challenge in _resolution.ChallengesFailed)
                    _canvas.WriteText($"  {challenge.Name}", sidebarCol, row++, LowEffortCanvas.RegularTextColor);
            }

            // Draw the map and everything on it.
            WriteBannerText();
            _canvas.PaintTerrain(_resolution.Terrain, null, 0, _topMapRow);
            _canvas.PaintSpawnPoints(_resolution.Terrain, 0, _topMapRow);
            _canvas.PaintGoalPoints(_resolution.Terrain, 0, _topMapRow);
            _canvas.PaintEntities(_entities.Values, 0, _topMapRow);
            _canvas.PaintDamageIndicators(_rencentDamageEvents, 0, _topMapRow);

            _canvas.WriteText("Press any key to continue", 0, _statusBarRow, LowEffortCanvas.RegularTextColor);
            _canvas.EndFrame();

            _canvas.ReadKey();
        }

        private void WriteBannerText()
        {
            if (_resolution.BannerText != null)
            {
                for (int row=0; row<_resolution.BannerText.Count; ++row)
                    _canvas.WriteText(_resolution.BannerText[row], 0, row, LowEffortCanvas.RegularTextColor);
            }
        }
    }
}
