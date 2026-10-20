using System.Collections.Generic;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Logic
{
    public class WireNet
    {
        public int Id { get; set; }
        public sbyte CurrentTrit { get; set; } = 0;
        public readonly List<int> WireVoxelCoords = new(64);
        public readonly List<int> ConnectedGates = new(16);
    }

    public class WireNetManager
    {
        private readonly WorldGrid _world;
        private readonly Dictionary<int, WireNet> _networks = new();
        private int _nextNetId = 1;

        // BFS traversal buffers to avoid allocations
        private readonly Queue<int> _bfsQueue = new(256);
        private readonly HashSet<int> _visited = new(256);

        public WireNetManager(WorldGrid world)
        {
            _world = world;
        }

        public void RebuildNetworkAt(int startX, int startY, int startZ, FastQueue dirtyQueue, bool[] inQueueFlags)
        {
            _bfsQueue.Clear();
            _visited.Clear();

            int startPacked = FastQueue.PackCoord(startX, startY, startZ);
            _bfsQueue.Enqueue(startPacked);
            _visited.Add(startPacked);

            var net = new WireNet();
            net.Id = _nextNetId++;
            sbyte drivingSignal = 0;

            while (_bfsQueue.Count > 0)
            {
                int curr = _bfsQueue.Dequeue();
                FastQueue.UnpackCoord(curr, out int cx, out int cy, out int cz);

                ushort id = _world.GetBlockId(cx, cy, cz);
                if (id == (ushort)BlockType.RGBWire)
                {
                    net.WireVoxelCoords.Add(curr);

                    // Inspect 6 neighbors for other wires, sources, or gate inputs
                    for (byte d = 0; d < 6; d++)
                    {
                        int nx = cx + Direction.OffsetX[d];
                        int ny = cy + Direction.OffsetY[d];
                        int nz = cz + Direction.OffsetZ[d];

                        if (!_world.IsInsideWorld(nx, ny, nz)) continue;

                        ushort nId = _world.GetBlockId(nx, ny, nz);
                        if (nId == (ushort)BlockType.RGBWire)
                        {
                            int nPacked = FastQueue.PackCoord(nx, ny, nz);
                            if (_visited.Add(nPacked))
                            {
                                _bfsQueue.Enqueue(nPacked);
                            }
                        }
                        else if (nId == (ushort)BlockType.TritSource)
                        {
                            // Driving power source
                            sbyte srcTrit = _world.GetTritState(nx, ny, nz);
                            if (srcTrit != 0) drivingSignal = srcTrit;
                        }
                        else if (BlockCategory.IsSimulated((BlockType)nId))
                        {
                            // Gate output feeding wire, or gate/motor/piston input consuming wire
                            byte facing = _world.GetBlockMeta(nx, ny, nz);
                            // If this gate faces towards this wire, it is driving the wire!
                            int outX = nx + Direction.OffsetX[facing];
                            int outY = ny + Direction.OffsetY[facing];
                            int outZ = nz + Direction.OffsetZ[facing];

                            if (outX == cx && outY == cy && outZ == cz && BlockCategory.IsLogic((BlockType)nId))
                            {
                                sbyte gateOutput = _world.GetTritState(nx, ny, nz);
                                if (gateOutput != 0) drivingSignal = gateOutput;
                            }
                            else
                            {
                                // Otherwise, this gate/motor/piston receives input from the wire
                                int gatePacked = FastQueue.PackCoord(nx, ny, nz);
                                if (!net.ConnectedGates.Contains(gatePacked))
                                {
                                    net.ConnectedGates.Add(gatePacked);
                                }
                            }
                        }
                    }
                }
            }

            net.CurrentTrit = drivingSignal;
            _networks[net.Id] = net;

            // Apply network ID and driving signal to all wires in the network
            for (int i = 0; i < net.WireVoxelCoords.Count; i++)
            {
                FastQueue.UnpackCoord(net.WireVoxelCoords[i], out int wx, out int wy, out int wz);
                _world.SetTritState(wx, wy, wz, drivingSignal);
            }

            // Enqueue all connected consumer gates to be evaluated in the simulation
            for (int i = 0; i < net.ConnectedGates.Count; i++)
            {
                int gPacked = net.ConnectedGates[i];
                FastQueue.UnpackCoord(gPacked, out int gx, out int gy, out int gz);
                uint gIdx = _world.GetGlobalLogicIndex(gx, gy, gz);

                if (!inQueueFlags[gIdx])
                {
                    inQueueFlags[gIdx] = true;
                    dirtyQueue.TryEnqueue(gPacked);
                }
            }
        }

        public void PropagateSignalFromSource(int srcX, int srcY, int srcZ, sbyte newSignal, FastQueue dirtyQueue, bool[] inQueueFlags)
        {
            // Check all 6 adjacent directions for RGB wires
            for (byte d = 0; d < 6; d++)
            {
                int nx = srcX + Direction.OffsetX[d];
                int ny = srcY + Direction.OffsetY[d];
                int nz = srcZ + Direction.OffsetZ[d];

                if (_world.IsInsideWorld(nx, ny, nz) && _world.GetBlockId(nx, ny, nz) == (ushort)BlockType.RGBWire)
                {
                    RebuildNetworkAt(nx, ny, nz, dirtyQueue, inQueueFlags);
                }
            }
        }
    }
}
