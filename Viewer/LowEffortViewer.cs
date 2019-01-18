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
    /// Class that shows battle results by animating ASCII symbols on a terminal window.
    /// </summary>
    public class LowEffortViewer
    {
        public double FrameTimeSeconds { get; set; } = 0.2;

        public double DamageDisplayTimeSeconds { get; set; } = 0.2;

        /// <summary>
        /// Shows a battle result by animating ASCII symbols on a terminal window.
        /// </summary>
        public void ShowBattleResolution(BattleResolution resolution)
        {
            // Clear screen
            _canvas.Init();

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
            _exitRequested = false;
            while ((eventQueue.Count>0 || firstPass) && !_exitRequested)
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

                _canvas.BeginFrame();

                // Draw text events first.  Otherwise if they go past the edge of the screen, they trash part of the map.
                _canvas.WriteTextEvents(_recentTextEvents, resolution.Terrain.Width + 2, 0);

                // Draw the map and everything on it.
                _canvas.PaintTerrain(resolution.Terrain, 0, 0);
                _canvas.PaintSpawnPoints(resolution.Terrain, 0, 0);
                _canvas.PaintGoalPoints(resolution.Terrain, 0, 0);
                _canvas.PaintEntities(_entities.Values, 0, 0);
                _canvas.PaintDamageIndicators(_rencentDamageEvents, 0, 0);

                _canvas.EndFrame();

                ProcessUserInput();

                firstPass = false;
                time += this.FrameTimeSeconds;

                var processFrameTimeMS = frameTimer.ElapsedMilliseconds;
                var sleepTimeMS = Math.Max(0, (int)(this.FrameTimeSeconds*1000 - processFrameTimeMS));
                System.Threading.Thread.Sleep(sleepTimeMS);
            }

            _canvas.Shutdown();
        }

        private readonly LowEffortCanvas _canvas = new LowEffortCanvas();
        private Dictionary<string,ViewEntity> _entities;
        private Queue<BattleEvent> _rencentDamageEvents;
        private Queue<BattleEvent> _recentTextEvents;
        private Dictionary<string,UnitCharacteristics> _unitTypeMap;
        private bool _exitRequested = false;

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

        private void ProcessUserInput()
        {
            var keyInfo = _canvas.ReadKeyWithoutBlocking();
            if (keyInfo.HasValue)
            {
                if (keyInfo.Value.Key == ConsoleKey.C && (keyInfo.Value.Modifiers & ConsoleModifiers.Control)!=0)
                    _exitRequested = true;

                // TODO: add speed, rewind, etc.
            }
        }
    }
}
