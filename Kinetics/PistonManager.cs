using System;
using System.Collections.Generic;
using BinaryTernaryCraft.Logic;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Kinetics
{
    public class PistonManager
    {
        public const int MaxPushBlocks = 12;

        private readonly WorldGrid _world;
        private readonly SimulationEngine? _simEngine;

        public PistonManager(WorldGrid world, SimulationEngine? simEngine)
        {
            _world = world;
            _simEngine = simEngine;
        }

        public static int TritToExtension(sbyte trit)
        {
            return trit switch
            {
                -1 => 0, // Retracted
                1 => 2,  // Fully extended (2 blocks)
                _ => 1   // Middle (1 block)
            };
        }

        public bool SetPistonState(int px, int py, int pz, sbyte tritSignal)
        {
            if (!_world.IsInsideWorld(px, py, pz)) return false;

            ushort blockId = _world.GetBlockId(px, py, pz);
            if (blockId != (ushort)BlockType.TernaryPiston) return false;

            byte facing = _world.GetBlockMeta(px, py, pz);
            int currentExtension = _world.GetTimerConfig(px, py, pz);
            int targetExtension = TritToExtension(tritSignal);

            if (currentExtension == targetExtension) return true;

            int dx = Direction.OffsetX[facing];
            int dy = Direction.OffsetY[facing];
            int dz = Direction.OffsetZ[facing];

            // Extension (Push forward)
            if (targetExtension > currentExtension)
            {
                int steps = targetExtension - currentExtension;
                for (int s = 0; s < steps; s++)
                {
                    int pushOriginX = px + dx * (currentExtension + s + 1);
                    int pushOriginY = py + dy * (currentExtension + s + 1);
                    int pushOriginZ = pz + dz * (currentExtension + s + 1);

                    if (!TryPushLine(pushOriginX, pushOriginY, pushOriginZ, dx, dy, dz))
                    {
                        // Obstructed, cannot push further
                        _world.SetTimerConfig(px, py, pz, (ushort)(currentExtension + s));
                        return false;
                    }
                }
            }

            // Successfully reached target extension
            _world.SetTimerConfig(px, py, pz, (ushort)targetExtension);
            _world.SetTritState(px, py, pz, tritSignal);

            return true;
        }

        private bool TryPushLine(int startX, int startY, int startZ, int dx, int dy, int dz)
        {
            // Collect blocks in line until an Air block is reached
            var blocksToPush = new List<(int x, int y, int z, ushort id, sbyte trit, byte meta, ushort timer, int net)>(MaxPushBlocks);

            int cx = startX;
            int cy = startY;
            int cz = startZ;

            while (blocksToPush.Count <= MaxPushBlocks)
            {
                if (!_world.IsInsideWorld(cx, cy, cz))
                {
                    // Hit world boundary
                    return false;
                }

                ushort id = _world.GetBlockId(cx, cy, cz);
                if (id == (ushort)BlockType.Air)
                {
                    // Empty space found! We can push all accumulated blocks
                    break;
                }

                // If block is immovable (e.g. Bedrock if present)
                // In our world, all standard blocks can be pushed except boundary
                sbyte trit = _world.GetTritState(cx, cy, cz);
                byte meta = _world.GetBlockMeta(cx, cy, cz);
                ushort timer = _world.GetTimerConfig(cx, cy, cz);
                int net = _world.GetNetworkId(cx, cy, cz);

                blocksToPush.Add((cx, cy, cz, id, trit, meta, timer, net));

                cx += dx;
                cy += dy;
                cz += dz;
            }

            if (blocksToPush.Count > MaxPushBlocks)
            {
                // Push limit exceeded
                return false;
            }

            // Move blocks forward in reverse order (from furthest to nearest)
            for (int i = blocksToPush.Count - 1; i >= 0; i--)
            {
                var b = blocksToPush[i];
                int targetX = b.x + dx;
                int targetY = b.y + dy;
                int targetZ = b.z + dz;

                _world.SetBlock(targetX, targetY, targetZ, b.id, b.trit, b.meta, b.timer, b.net);
                _simEngine?.NotifyBlockPlacedOrBroken(targetX, targetY, targetZ, b.id, b.trit);
            }

            // Clear the vacated block at the start of the push
            _world.SetBlock(startX, startY, startZ, (ushort)BlockType.Air);
            _simEngine?.NotifyBlockPlacedOrBroken(startX, startY, startZ, (ushort)BlockType.Air, 0);

            return true;
        }
    }
}
