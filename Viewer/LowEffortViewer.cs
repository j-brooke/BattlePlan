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

        public void Show(BattleResolution resolution)
        {
            // Clear screen
            Console.Write("\u001B[2J");

            _entities = new Dictionary<string, ViewEntity>();
            _terrainBuffer = MakeTerrainBuffer(resolution.Terrain);
            _frameBuffer = new char[_terrainBuffer.GetLength(0),_terrainBuffer.GetLength(1)];
            _rencentDamageEvents = new Queue<BattleEvent>();

            var eventQueue = new Queue<BattleEvent>(resolution.Events.OrderBy( (evt) => evt.Time ) );
            double time = 0.0;
            bool firstPass = true;
            while (eventQueue.Count>0 || firstPass)
            {
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

                Array.Copy(_terrainBuffer, _frameBuffer, _terrainBuffer.Length);
                AddEntitiesToFrameBuffer();

                RemoveOldDamageEvents(time);
                AddDamageToFrameBuffer();

                DrawBuffer(_frameBuffer);

                firstPass = false;
                time += this.FrameTimeSeconds;
                System.Threading.Thread.Sleep((int)(this.FrameTimeSeconds*1000));
            }
        }

        private char[,] _terrainBuffer;
        private char[,] _frameBuffer;
        private Dictionary<string,ViewEntity> _entities;
        private Queue<BattleEvent> _rencentDamageEvents;

        private char[,] MakeTerrainBuffer(Terrain terrain)
        {
            var buffer = new char[terrain.Width, terrain.Height];
            for (var x=0; x<terrain.Width; ++x)
            {
                for (var y=0; y<terrain.Height; ++y)
                {
                    var tileCharacteristics = terrain.GetTile(x,y);
                    char tileSymbol = ' ';
                    if (tileCharacteristics.Appearance!=null && tileCharacteristics.Appearance.Length>0)
                        tileSymbol = tileCharacteristics.Appearance[0];
                    buffer[x,y] = tileSymbol;
                }
            }

            var allSpawnPoints = terrain.SpawnPointsMap.SelectMany( (spawnList) => spawnList.Value ).ToList();
            foreach (var spawnPoint in allSpawnPoints)
                buffer[spawnPoint.X,spawnPoint.Y] = 'O';

            var allGoals = terrain.GoalPointsMap.SelectMany( (goalList) => goalList.Value ).ToList();
            foreach (var goal in allGoals)
                buffer[goal.X,goal.Y] = 'X';

            return buffer;
        }

        private void DrawBuffer(char[,] buffer)
        {
            var stringBuff = new System.Text.StringBuilder();

            // Go to top left of screen
            stringBuff.Append("\u001B[1;1H");

            for (int y=0; y<buffer.GetLength(1); ++y)
            {
                for (int x=0; x<buffer.GetLength(0); ++x)
                    stringBuff.Append(buffer[x,y]);
                stringBuff.AppendLine();
            }

            Console.WriteLine(stringBuff.ToString());
        }

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
            }
        }

        private void ProcessSpawnEvent(BattleEvent evt)
        {
            Debug.Assert(evt.Type==BattleEventType.Spawn);

            var newEnt = new ViewEntity()
            {
                Id = evt.SourceEntity,
                TeamId = evt.SourceTeamId,
                Class = evt.SourceClass.Value,
                Position = evt.SourceLocation.Value,
            };

            switch (evt.SourceClass.Value)
            {
                case UnitClass.AttackerGrunt:
                    newEnt.Symbol = 'T';
                    break;
                case UnitClass.DefenderArcher:
                    newEnt.Symbol = '{';
                    break;
                default:
                    newEnt.Symbol = '?';
                    break;
            }

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
                _rencentDamageEvents.Enqueue(evt);
        }

        private void AddEntitiesToFrameBuffer()
        {
            foreach (var ent in _entities.Values)
                _frameBuffer[ent.Position.X,ent.Position.Y] = ent.Symbol;
        }

        private void AddDamageToFrameBuffer()
        {
            foreach (var dmgEvt in _rencentDamageEvents)
            {
                var position = dmgEvt.TargetLocation.Value;
                _frameBuffer[position.X, position.Y] = '*';
            }
        }

        private void RemoveOldDamageEvents(double currentTime)
        {
            var thresholdTime = currentTime - this.DamageDisplayTimeSeconds;

            bool keepLooking = true;
            while (_rencentDamageEvents.Count>0 && keepLooking)
            {
                var evt = _rencentDamageEvents.Peek();
                if (evt.Time<=thresholdTime)
                {
                    _rencentDamageEvents.Dequeue();
                }
                else
                {
                    keepLooking = false;
                }
            }
        }
    }
}
