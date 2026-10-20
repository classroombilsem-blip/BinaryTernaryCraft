using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BinaryTernaryCraft.Core;
using BinaryTernaryCraft.Kinetics;
using BinaryTernaryCraft.Rendering;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Logic
{
    public class SimulationEngine : IDisposable
    {
        private readonly WorldGrid _world;
        private readonly WireNetManager _wireNetManager;

        public KineticNetworkManager KineticMgr { get; }
        public PistonManager PistonMgr { get; }
        public volatile bool HasPendingInstanceUpdate = false;

        private Thread? _simThread;
        private volatile bool _isRunning = false;

        public long CurrentTick { get; private set; } = 0;
        public double MeasuredTPS { get; private set; } = 0.0;
        public int ActiveDirtyCount => _currentTickQueue.Count;
        public int ScheduledEventCount => _scheduledQueue.Count;

        // Double-Buffered Fast Dirty Queues (Zero-Allocation)
        private FastQueue _currentTickQueue;
        private FastQueue _nextTickQueue;
        private readonly bool[] _inQueueFlags;

        // Priority Queue for Aeronautics Timers (CPU cost = 0 on inactive ticks)
        private readonly PriorityQueue<ScheduledLogicEvent, long> _scheduledQueue = new(1024);

        // Double-buffered state change synchronization for GPU SSBO
        private readonly int[] _ssboTritBuffer;
        private readonly object _ssboLock = new();
        private volatile bool _hasPendingSSBOUpdate = false;

        public SimulationEngine(WorldGrid world)
        {
            _world = world;
            _wireNetManager = new WireNetManager(world);
            KineticMgr = new KineticNetworkManager(world);
            PistonMgr = new PistonManager(world, this);

            int totalVoxels = EngineConfig.TotalWorldVoxels;
            _inQueueFlags = new bool[totalVoxels];
            _currentTickQueue = new FastQueue(65536);
            _nextTickQueue = new FastQueue(65536);

            _ssboTritBuffer = new int[totalVoxels];
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _simThread = new Thread(SimulationLoop)
            {
                Name = "SimulationThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _simThread.Start();
        }

        public void EnqueueBlock(int x, int y, int z)
        {
            if (!_world.IsInsideWorld(x, y, z)) return;
            uint gIdx = _world.GetGlobalLogicIndex(x, y, z);

            lock (_inQueueFlags)
            {
                if (!_inQueueFlags[gIdx])
                {
                    _inQueueFlags[gIdx] = true;
                    int packed = FastQueue.PackCoord(x, y, z);
                    _nextTickQueue.TryEnqueue(packed);
                }
            }
        }

        public void NotifyBlockPlacedOrBroken(int x, int y, int z, ushort blockId, sbyte trit)
        {
            // If RGB wire, rebuild wire network
            if (blockId == (ushort)BlockType.RGBWire)
            {
                _wireNetManager.RebuildNetworkAt(x, y, z, _nextTickQueue, _inQueueFlags);
            }
            else if (blockId == (ushort)BlockType.TritSource)
            {
                _wireNetManager.PropagateSignalFromSource(x, y, z, trit, _nextTickQueue, _inQueueFlags);
            }
            else if (BlockCategory.IsKinetic((BlockType)blockId))
            {
                KineticMgr.RebuildNetworkAt(x, y, z);
                HasPendingInstanceUpdate = true;
            }

            // Enqueue all 6 neighbors to recheck their inputs / kinetics
            for (byte d = 0; d < 6; d++)
            {
                int nx = x + Direction.OffsetX[d];
                int ny = y + Direction.OffsetY[d];
                int nz = z + Direction.OffsetZ[d];
                if (_world.IsInsideWorld(nx, ny, nz))
                {
                    ushort nId = _world.GetBlockId(nx, ny, nz);
                    if (BlockCategory.IsSimulated((BlockType)nId))
                    {
                        EnqueueBlock(nx, ny, nz);
                    }
                    if (BlockCategory.IsKinetic((BlockType)nId))
                    {
                        KineticMgr.RebuildNetworkAt(nx, ny, nz);
                        HasPendingInstanceUpdate = true;
                    }
                }
            }
        }

        private void SimulationLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            long lastTimeTicks = stopwatch.ElapsedTicks;
            long tpsCounterTicks = stopwatch.ElapsedTicks;
            int ticksInCurrentSecond = 0;

            while (_isRunning)
            {
                double targetTPS = Math.Clamp(EngineConfig.TargetTPS, 1.0, 10000.0);
                double tickIntervalTicks = (double)Stopwatch.Frequency / targetTPS;

                long now = stopwatch.ElapsedTicks;
                double elapsed = now - lastTimeTicks;

                // Handle Pausing & Single-stepping
                bool shouldTick = false;
                if (!EngineConfig.SimulationPaused)
                {
                    if (elapsed >= tickIntervalTicks)
                    {
                        shouldTick = true;
                        lastTimeTicks = now;
                    }
                }
                else if (EngineConfig.StepSingleTick)
                {
                    shouldTick = true;
                    EngineConfig.StepSingleTick = false;
                    lastTimeTicks = now;
                }

                if (shouldTick)
                {
                    PerformSimulationTick();
                    ticksInCurrentSecond++;
                }
                else
                {
                    // Minimal yield to prevent burning 100% CPU when waiting at lower TPS
                    if (targetTPS <= 2000.0 && (tickIntervalTicks - elapsed) > (Stopwatch.Frequency / 5000.0))
                    {
                        Thread.Yield();
                    }
                }

                // Calculate Measured Live TPS every 250ms
                if (now - tpsCounterTicks >= Stopwatch.Frequency / 4)
                {
                    double timeSec = (double)(now - tpsCounterTicks) / Stopwatch.Frequency;
                    MeasuredTPS = ticksInCurrentSecond / timeSec;
                    ticksInCurrentSecond = 0;
                    tpsCounterTicks = now;
                }
            }
        }

        private void PerformSimulationTick()
        {
            CurrentTick++;

            // 1. Process Scheduled Tick Events (Aeronautics Timers)
            while (_scheduledQueue.Count > 0)
            {
                if (_scheduledQueue.TryPeek(out var nextEvent, out long execTick))
                {
                    if (execTick <= CurrentTick)
                    {
                        _scheduledQueue.Dequeue();
                        ApplyScheduledEvent(nextEvent);
                    }
                    else
                    {
                        break; // Top of priority queue is in future -> 0 CPU cost!
                    }
                }
            }

            // 2. Swap Dirty Queues
            FastQueue activeQueue;
            lock (_inQueueFlags)
            {
                activeQueue = _nextTickQueue;
                _nextTickQueue = _currentTickQueue;
                _currentTickQueue = activeQueue;
                _nextTickQueue.Clear();
            }

            if (activeQueue.Count == 0) return;

            // 3. Evaluate Logic Blocks in Active Queue
            for (int i = 0; i < activeQueue.Count; i++)
            {
                int packed = activeQueue.Get(i);
                FastQueue.UnpackCoord(packed, out int x, out int y, out int z);

                uint gIdx = _world.GetGlobalLogicIndex(x, y, z);
                _inQueueFlags[gIdx] = false;

                EvaluateBlockLogic(x, y, z);
            }
        }

        private void ApplyScheduledEvent(ScheduledLogicEvent evt)
        {
            if (!_world.IsInsideWorld(evt.X, evt.Y, evt.Z)) return;

            sbyte currentTrit = _world.GetTritState(evt.X, evt.Y, evt.Z);
            if (currentTrit != evt.TargetTrit)
            {
                _world.SetTritState(evt.X, evt.Y, evt.Z, evt.TargetTrit);
                MarkSSBODirty(evt.X, evt.Y, evt.Z, evt.TargetTrit);

                // Notify neighbors
                EnqueueBlockNeighbors(evt.X, evt.Y, evt.Z);
            }
        }

        private void EvaluateBlockLogic(int x, int y, int z)
        {
            ushort blockId = _world.GetBlockId(x, y, z);
            if (!BlockCategory.IsSimulated((BlockType)blockId)) return;

            byte facing = _world.GetBlockMeta(x, y, z);
            sbyte currentTrit = _world.GetTritState(x, y, z);
            ushort timerConfig = _world.GetTimerConfig(x, y, z);
            if (timerConfig == 0) timerConfig = 1;

            // Inputs
            byte rearDir = Direction.Opposite(facing);
            sbyte inRear = GetSignalFrom(x + Direction.OffsetX[rearDir], y + Direction.OffsetY[rearDir], z + Direction.OffsetZ[rearDir]);

            // Side inputs for 2-input gates
            byte leftDir = GetPerpendicularLeft(facing);
            byte rightDir = Direction.Opposite(leftDir);
            sbyte inLeft = GetSignalFrom(x + Direction.OffsetX[leftDir], y + Direction.OffsetY[leftDir], z + Direction.OffsetZ[leftDir]);
            sbyte inRight = GetSignalFrom(x + Direction.OffsetX[rightDir], y + Direction.OffsetY[rightDir], z + Direction.OffsetZ[rightDir]);

            sbyte newOutput = currentTrit;

            switch ((BlockType)blockId)
            {
                case BlockType.RGBWire:
                    // Wires propagate driving network signal
                    break;

                case BlockType.NotGate:
                    newOutput = TernaryGateLogic.Not(inRear);
                    break;

                case BlockType.CycleGate:
                    newOutput = TernaryGateLogic.Cycle(inRear);
                    break;

                case BlockType.AntiCycleGate:
                    newOutput = TernaryGateLogic.AntiCycle(inRear);
                    break;

                case BlockType.MinGate:
                    newOutput = TernaryGateLogic.Min(inLeft, inRight);
                    break;

                case BlockType.MaxGate:
                    newOutput = TernaryGateLogic.Max(inLeft, inRight);
                    break;

                case BlockType.SumGate:
                    newOutput = TernaryGateLogic.Sum(inLeft, inRight);
                    break;

                case BlockType.ConsensusGate:
                    newOutput = TernaryGateLogic.Consensus(inLeft, inRight);
                    break;

                // Aeronautics Blocks
                case BlockType.PulseGenerator:
                    // Detect edge transition on input (0 -> +1 or 0 -> -1)
                    if (inRear != 0 && currentTrit == 0)
                    {
                        newOutput = inRear;
                        _scheduledQueue.Enqueue(
                            new ScheduledLogicEvent(x, y, z, 0, ScheduledEventType.PulseFinish),
                            CurrentTick + timerConfig);
                    }
                    break;

                case BlockType.PulseExtender:
                    // Extends any arriving signal for timerConfig ticks
                    if (inRear != 0)
                    {
                        newOutput = inRear;
                        _scheduledQueue.Enqueue(
                            new ScheduledLogicEvent(x, y, z, 0, ScheduledEventType.ExtenderFinish),
                            CurrentTick + timerConfig);
                    }
                    break;

                case BlockType.ToggleLatch:
                    // When input pulses (+1), toggles between 0 and +1. When input is (-1), resets to -1.
                    if (inRear == 1 && currentTrit != 1)
                    {
                        newOutput = (sbyte)(currentTrit == 0 ? 1 : 0);
                    }
                    else if (inRear == -1)
                    {
                        newOutput = -1;
                    }
                    break;

                case BlockType.TritLatch:
                    // Gated memory cell: inRear is Data (D), inLeft is Clock (CLK)
                    if (inLeft == 1) // Write
                    {
                        newOutput = inRear;
                    }
                    else if (inLeft == -1) // Clear
                    {
                        newOutput = 0;
                    }
                    // inLeft == 0: Hold current value
                    break;

                case BlockType.RGBPixel:
                    newOutput = inRear;
                    break;

                // Kinetics Integration
                case BlockType.ElectricMotor:
                    newOutput = inRear;
                    if (newOutput != currentTrit)
                    {
                        KineticMgr.UpdateMotorSignal(x, y, z, newOutput);
                    }
                    break;

                case BlockType.TernaryPiston:
                    newOutput = inRear;
                    if (newOutput != currentTrit)
                    {
                        bool moved = PistonMgr.SetPistonState(x, y, z, newOutput);
                        if (moved)
                        {
                            HasPendingInstanceUpdate = true;
                        }
                    }
                    break;
            }

            if (newOutput != currentTrit)
            {
                _world.SetTritState(x, y, z, newOutput);
                MarkSSBODirty(x, y, z, newOutput);
                EnqueueBlockNeighbors(x, y, z);
            }
        }

        private sbyte GetSignalFrom(int nx, int ny, int nz)
        {
            if (!_world.IsInsideWorld(nx, ny, nz)) return 0;
            return _world.GetTritState(nx, ny, nz);
        }

        private static byte GetPerpendicularLeft(byte facing)
        {
            return facing switch
            {
                Direction.North => Direction.West,
                Direction.South => Direction.East,
                Direction.West => Direction.South,
                Direction.East => Direction.North,
                Direction.Up => Direction.West,
                _ => Direction.East
            };
        }

        private void EnqueueBlockNeighbors(int x, int y, int z)
        {
            for (byte d = 0; d < 6; d++)
            {
                int nx = x + Direction.OffsetX[d];
                int ny = y + Direction.OffsetY[d];
                int nz = z + Direction.OffsetZ[d];

                if (_world.IsInsideWorld(nx, ny, nz))
                {
                    ushort id = _world.GetBlockId(nx, ny, nz);
                    if (id == (ushort)BlockType.RGBWire)
                    {
                        _wireNetManager.RebuildNetworkAt(nx, ny, nz, _nextTickQueue, _inQueueFlags);
                    }
                    else if (BlockCategory.IsSimulated((BlockType)id))
                    {
                        EnqueueBlock(nx, ny, nz);
                    }
                }
            }
        }

        private void MarkSSBODirty(int x, int y, int z, sbyte trit)
        {
            uint gIdx = _world.GetGlobalLogicIndex(x, y, z);
            lock (_ssboLock)
            {
                if (gIdx < _ssboTritBuffer.Length)
                {
                    _ssboTritBuffer[gIdx] = trit;
                    _hasPendingSSBOUpdate = true;
                }
            }
        }

        /// <summary>
        /// Called each frame from Render Thread to upload modified Trit states and Kinetic speeds into GPU SSBO.
        /// </summary>
        public void SyncToSSBO(SSBOManager ssbo)
        {
            if (_hasPendingSSBOUpdate)
            {
                lock (_ssboLock)
                {
                    ssbo.UpdateAllStates(_ssboTritBuffer);
                    _hasPendingSSBOUpdate = false;
                }
            }

            // Sync kinetic speeds to Binding 1 SSBO
            KineticMgr.SyncToSSBO(ssbo);
        }

        public void Dispose()
        {
            _isRunning = false;
            _simThread?.Join(100);
        }
    }
}
