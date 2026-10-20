using System;
using System.Runtime.CompilerServices;
using BinaryTernaryCraft.Core;

namespace BinaryTernaryCraft.World
{
    public class WorldGrid
    {
        public readonly int ChunksX;
        public readonly int ChunksY;
        public readonly int ChunksZ;

        public readonly int VoxelsX;
        public readonly int VoxelsY;
        public readonly int VoxelsZ;

        private readonly Chunk[,,] _chunks;

        public WorldGrid(int chunksX, int chunksY, int chunksZ)
        {
            ChunksX = chunksX;
            ChunksY = chunksY;
            ChunksZ = chunksZ;

            VoxelsX = chunksX * Chunk.Size;
            VoxelsY = chunksY * Chunk.Size;
            VoxelsZ = chunksZ * Chunk.Size;

            _chunks = new Chunk[ChunksX, ChunksY, ChunksZ];
            for (int cx = 0; cx < ChunksX; cx++)
            {
                for (int cy = 0; cy < ChunksY; cy++)
                {
                    for (int cz = 0; cz < ChunksZ; cz++)
                    {
                        _chunks[cx, cy, cz] = new Chunk(cx, cy, cz);
                    }
                }
            }

            InitializeDefaultWorld();
        }

        private void InitializeDefaultWorld()
        {
            // Create a nice ground floor (y = 0..3) with Stone
            int groundHeight = 2;
            for (int x = 0; x < VoxelsX; x++)
            {
                for (int z = 0; z < VoxelsZ; z++)
                {
                    for (int y = 0; y < groundHeight; y++)
                    {
                        SetBlock(x, y, z, (ushort)BlockType.Stone);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInsideWorld(int x, int y, int z)
        {
            return x >= 0 && x < VoxelsX &&
                   y >= 0 && y < VoxelsY &&
                   z >= 0 && z < VoxelsZ;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Chunk? GetChunkAtVoxel(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return null;
            return _chunks[x >> 4, y >> 4, z >> 4];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Chunk? GetChunk(int cx, int cy, int cz)
        {
            if (cx < 0 || cx >= ChunksX || cy < 0 || cy >= ChunksY || cz < 0 || cz >= ChunksZ)
                return null;
            return _chunks[cx, cy, cz];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetGlobalLogicIndex(int x, int y, int z)
        {
            int cx = x >> 4;
            int cy = y >> 4;
            int cz = z >> 4;

            int lx = x & 15;
            int ly = y & 15;
            int lz = z & 15;

            int chunkLinearIndex = (cy * ChunksZ + cz) * ChunksX + cx;
            return (uint)(chunkLinearIndex * Chunk.VoxelCount + Chunk.GetIndex(lx, ly, lz));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetBlockId(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return (ushort)BlockType.Air;
            return _chunks[x >> 4, y >> 4, z >> 4].GetBlockId(x & 15, y & 15, z & 15);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte GetTritState(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return 0;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            return c.TritState[Chunk.GetIndex(x & 15, y & 15, z & 15)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTritState(int x, int y, int z, sbyte trit)
        {
            if (!IsInsideWorld(x, y, z)) return;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            c.TritState[Chunk.GetIndex(x & 15, y & 15, z & 15)] = trit;
            // NOTE: Changing TritState DOES NOT mark chunk mesh dirty! SSBO updates it dynamically.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetBlockMeta(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return 0;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            return c.BlockMeta[Chunk.GetIndex(x & 15, y & 15, z & 15)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetTimerConfig(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return 0;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            return c.BlockTimerConfig[Chunk.GetIndex(x & 15, y & 15, z & 15)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTimerConfig(int x, int y, int z, ushort timer)
        {
            if (!IsInsideWorld(x, y, z)) return;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            c.BlockTimerConfig[Chunk.GetIndex(x & 15, y & 15, z & 15)] = timer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNetworkId(int x, int y, int z)
        {
            if (!IsInsideWorld(x, y, z)) return 0;
            var c = _chunks[x >> 4, y >> 4, z >> 4];
            return c.NetworkId[Chunk.GetIndex(x & 15, y & 15, z & 15)];
        }

        public void SetBlock(int x, int y, int z, ushort id, sbyte trit = 0, byte meta = 0, ushort timer = 0, int netId = 0)
        {
            if (!IsInsideWorld(x, y, z)) return;

            int cx = x >> 4;
            int cy = y >> 4;
            int cz = z >> 4;

            int lx = x & 15;
            int ly = y & 15;
            int lz = z & 15;

            var chunk = _chunks[cx, cy, cz];
            chunk.SetBlock(lx, ly, lz, id, trit, meta, timer, netId);

            // Invalidate neighbor chunk meshes if on chunk boundary
            if (lx == 0 && cx > 0) _chunks[cx - 1, cy, cz].IsMeshDirty = true;
            if (lx == 15 && cx < ChunksX - 1) _chunks[cx + 1, cy, cz].IsMeshDirty = true;
            if (ly == 0 && cy > 0) _chunks[cx, cy - 1, cz].IsMeshDirty = true;
            if (ly == 15 && cy < ChunksY - 1) _chunks[cx, cy + 1, cz].IsMeshDirty = true;
            if (lz == 0 && cz > 0) _chunks[cx, cy, cz - 1].IsMeshDirty = true;
            if (lz == 15 && cz < ChunksZ - 1) _chunks[cx, cy, cz + 1].IsMeshDirty = true;
        }
    }
}
