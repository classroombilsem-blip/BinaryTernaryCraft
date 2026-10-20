using System;
using System.Runtime.CompilerServices;
using BinaryTernaryCraft.Core;

namespace BinaryTernaryCraft.World
{
    public class Chunk
    {
        public const int Size = EngineConfig.ChunkSize; // 16
        public const int VoxelCount = EngineConfig.ChunkVoxels; // 4096

        public readonly int ChunkX;
        public readonly int ChunkY;
        public readonly int ChunkZ;

        // Flat 1D data arrays (Zero-GC, cache friendly)
        public readonly ushort[] BlockId;
        public readonly sbyte[] TritState;
        public readonly byte[] BlockMeta;
        public readonly ushort[] BlockTimerConfig;
        public readonly int[] NetworkId;

        public bool IsMeshDirty { get; set; } = true;
        public bool IsEmpty { get; private set; } = true;

        public Chunk(int cx, int cy, int cz)
        {
            ChunkX = cx;
            ChunkY = cy;
            ChunkZ = cz;

            BlockId = new ushort[VoxelCount];
            TritState = new sbyte[VoxelCount];
            BlockMeta = new byte[VoxelCount];
            BlockTimerConfig = new ushort[VoxelCount];
            NetworkId = new int[VoxelCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetIndex(int lx, int ly, int lz)
        {
            return lx + (ly * 16) + (lz * 256);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetBlockId(int lx, int ly, int lz)
        {
            return BlockId[lx + (ly * 16) + (lz * 256)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBlock(int lx, int ly, int lz, ushort id, sbyte trit = 0, byte meta = 0, ushort timer = 0, int netId = 0)
        {
            int idx = lx + (ly * 16) + (lz * 256);
            BlockId[idx] = id;
            TritState[idx] = trit;
            BlockMeta[idx] = meta;
            BlockTimerConfig[idx] = timer;
            NetworkId[idx] = netId;

            IsMeshDirty = true;
            if (id != (ushort)BlockType.Air)
            {
                IsEmpty = false;
            }
        }
    }
}
