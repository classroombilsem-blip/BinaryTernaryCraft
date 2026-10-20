using System;
using System.Collections.Generic;
using System.Numerics;
using BinaryTernaryCraft.Logic;
using BinaryTernaryCraft.Rendering;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Kinetics
{
    public class KineticNetworkManager
    {
        public const float BaseMotorRPM = 16.0f; // Base angular speed (rad/s)
        public const int MaxKineticNetworks = 4096;

        private readonly WorldGrid _world;
        private readonly Dictionary<int, KineticNetwork> _networks = new();
        private readonly Dictionary<int, int> _coordToNetId = new();
        private int _nextNetId = 1;

        private readonly float[] _kineticSpeeds = new float[MaxKineticNetworks];
        private readonly object _lock = new();
        private volatile bool _hasPendingSSBOUpdate = false;

        // BFS traversal buffers to eliminate heap allocations during rebuild
        private readonly Queue<(int packedCoord, float ratio, byte axis)> _bfsQueue = new(256);
        private readonly HashSet<int> _visited = new(256);

        public KineticNetworkManager(WorldGrid world)
        {
            _world = world;
        }

        public float GetGearRatio(int x, int y, int z)
        {
            int packed = FastQueue.PackCoord(x, y, z);
            lock (_lock)
            {
                if (_coordToNetId.TryGetValue(packed, out int netId) &&
                    _networks.TryGetValue(netId, out var net))
                {
                    return net.GetRatio(packed);
                }
            }
            return 1.0f;
        }

        public void RebuildNetworkAt(int startX, int startY, int startZ)
        {
            if (!_world.IsInsideWorld(startX, startY, startZ)) return;

            ushort startId = _world.GetBlockId(startX, startY, startZ);
            if (!BlockCategory.IsKinetic((BlockType)startId)) return;

            lock (_lock)
            {
                _bfsQueue.Clear();
                _visited.Clear();

                int startPacked = FastQueue.PackCoord(startX, startY, startZ);
                byte startMeta = _world.GetBlockMeta(startX, startY, startZ);
                byte startAxis = (byte)(startMeta % 6);

                var net = new KineticNetwork(_nextNetId++);
                net.AddMember(startPacked, 1.0f);

                _bfsQueue.Enqueue((startPacked, 1.0f, startAxis));
                _visited.Add(startPacked);

                int? drivingMotor = null;
                sbyte motorSignal = 0;

                while (_bfsQueue.Count > 0)
                {
                    var (currPacked, currRatio, currAxis) = _bfsQueue.Dequeue();
                    FastQueue.UnpackCoord(currPacked, out int cx, out int cy, out int cz);

                    ushort currId = _world.GetBlockId(cx, cy, cz);
                    BlockType currType = (BlockType)currId;

                    if (currType == BlockType.ElectricMotor)
                    {
                        drivingMotor = currPacked;
                        byte motorFacing = _world.GetBlockMeta(cx, cy, cz);
                        byte rearDir = Direction.Opposite(motorFacing);
                        int rx = cx + Direction.OffsetX[rearDir];
                        int ry = cy + Direction.OffsetY[rearDir];
                        int rz = cz + Direction.OffsetZ[rearDir];

                        if (_world.IsInsideWorld(rx, ry, rz))
                        {
                            motorSignal = _world.GetTritState(rx, ry, rz);
                        }
                    }

                    // Check 6 adjacent neighbors for kinetic connections
                    for (byte d = 0; d < 6; d++)
                    {
                        int nx = cx + Direction.OffsetX[d];
                        int ny = cy + Direction.OffsetY[d];
                        int nz = cz + Direction.OffsetZ[d];

                        if (!_world.IsInsideWorld(nx, ny, nz)) continue;

                        ushort nId = _world.GetBlockId(nx, ny, nz);
                        if (!BlockCategory.IsKinetic((BlockType)nId)) continue;

                        int nPacked = FastQueue.PackCoord(nx, ny, nz);
                        if (_visited.Contains(nPacked)) continue;

                        byte nMeta = _world.GetBlockMeta(nx, ny, nz);
                        byte nAxis = (byte)(nMeta % 6);
                        BlockType nType = (BlockType)nId;

                        bool connected = false;
                        float childRatio = currRatio;

                        // Case 1: Shaft to Shaft or Motor to Shaft along rotation axis
                        bool isAlignedAlongAxis = (Direction.GetAxisVector(currAxis) == Direction.GetAxisVector(d));
                        if (isAlignedAlongAxis && Direction.GetAxisVector(currAxis) == Direction.GetAxisVector(nAxis))
                        {
                            // Direct axial coupling: same direction, same speed (1:1)
                            connected = true;
                            childRatio = currRatio;
                        }
                        // Case 2: Gear to Gear (teeth engagement)
                        else if ((currType == BlockType.KineticGear || currType == BlockType.KineticShaft) &&
                                 (nType == BlockType.KineticGear))
                        {
                            // If axes are parallel and blocks touch perpendicular to the axis:
                            if (Direction.GetAxisVector(currAxis) == Direction.GetAxisVector(nAxis) && !isAlignedAlongAxis)
                            {
                                connected = true;
                                // Teeth mesh reverses rotation direction!
                                childRatio = -currRatio;
                            }
                        }

                        if (connected)
                        {
                            _visited.Add(nPacked);
                            net.AddMember(nPacked, childRatio);
                            _bfsQueue.Enqueue((nPacked, childRatio, nAxis));
                        }
                    }
                }

                // Calculate base speed from driving motor
                if (drivingMotor.HasValue)
                {
                    net.DrivingMotorPackedCoord = drivingMotor.Value;
                    net.BaseSpeed = motorSignal * BaseMotorRPM;
                }
                else
                {
                    net.BaseSpeed = 0.0f;
                }

                // Register network and assign IDs to world chunk blocks
                _networks[net.Id] = net;
                foreach (var kvp in net.MemberRatios)
                {
                    int pCoord = kvp.Key;
                    _coordToNetId[pCoord] = net.Id;

                    FastQueue.UnpackCoord(pCoord, out int bx, out int by, out int bz);
                    var chunk = _world.GetChunkAtVoxel(bx, by, bz);
                    if (chunk != null)
                    {
                        chunk.NetworkId[Chunk.GetIndex(bx & 15, by & 15, bz & 15)] = net.Id;
                    }
                }

                // Update SSBO buffer speed for this network
                if (net.Id < MaxKineticNetworks)
                {
                    _kineticSpeeds[net.Id] = net.BaseSpeed;
                    _hasPendingSSBOUpdate = true;
                }
            }
        }

        public void UpdateMotorSignal(int mx, int my, int mz, sbyte tritSignal)
        {
            int packed = FastQueue.PackCoord(mx, my, mz);
            lock (_lock)
            {
                if (_coordToNetId.TryGetValue(packed, out int netId) &&
                    _networks.TryGetValue(netId, out var net))
                {
                    net.BaseSpeed = tritSignal * BaseMotorRPM;
                    if (net.Id < MaxKineticNetworks)
                    {
                        _kineticSpeeds[net.Id] = net.BaseSpeed;
                        _hasPendingSSBOUpdate = true;
                    }
                }
                else
                {
                    // Rebuild if network wasn't mapped yet
                    RebuildNetworkAt(mx, my, mz);
                }
            }
        }

        public void SyncToSSBO(SSBOManager ssbo)
        {
            if (!_hasPendingSSBOUpdate) return;

            lock (_lock)
            {
                ssbo.UpdateKineticSpeeds(_kineticSpeeds);
                _hasPendingSSBOUpdate = false;
            }
        }
    }
}
