using System.Collections.Generic;
using System.Numerics;
using BinaryTernaryCraft.Models;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Rendering
{
    public class ChunkMesher
    {
        private readonly Dictionary<BlockType, ParsedModel> _customModels;

        public ChunkMesher(Dictionary<BlockType, ParsedModel> customModels)
        {
            _customModels = customModels;
        }

        public void BuildMesh(
            Chunk chunk,
            WorldGrid world,
            List<VoxelVertex> outVertices,
            List<uint> outIndices)
        {
            outVertices.Clear();
            outIndices.Clear();

            int startX = chunk.ChunkX * Chunk.Size;
            int startY = chunk.ChunkY * Chunk.Size;
            int startZ = chunk.ChunkZ * Chunk.Size;

            for (int ly = 0; ly < Chunk.Size; ly++)
            {
                int wy = startY + ly;
                for (int lz = 0; lz < Chunk.Size; lz++)
                {
                    int wz = startZ + lz;
                    for (int lx = 0; lx < Chunk.Size; lx++)
                    {
                        int wx = startX + lx;
                        ushort blockId = chunk.GetBlockId(lx, ly, lz);
                        if (blockId == (ushort)BlockType.Air) continue;

                        BlockType type = (BlockType)blockId;
                        uint globalLogicIndex = world.GetGlobalLogicIndex(wx, wy, wz);

                        // If block has a custom .geo.json model, bake its STATIC parts into the chunk mesh
                        if (BlockCategory.HasCustomGeoModel(type) && _customModels.TryGetValue(type, out var geoModel))
                        {
                            if (geoModel.HasStaticMesh)
                            {
                                AppendStaticGeoModel(wx, wy, wz, globalLogicIndex, geoModel, outVertices, outIndices);
                            }
                            // Kinetic parts are drawn by InstancedKineticRenderer
                            continue;
                        }

                        // Otherwise, standard voxel block: culling faces against neighbor blocks
                        Vector3 blockPos = new Vector3(wx, wy, wz);

                        // West (-X)
                        if (IsFaceVisible(world, wx - 1, wy, wz))
                        {
                            AddVoxelFace(blockPos, Direction.West, type, globalLogicIndex, outVertices, outIndices);
                        }
                        // East (+X)
                        if (IsFaceVisible(world, wx + 1, wy, wz))
                        {
                            AddVoxelFace(blockPos, Direction.East, type, globalLogicIndex, outVertices, outIndices);
                        }
                        // Down (-Y)
                        if (IsFaceVisible(world, wx, wy - 1, wz))
                        {
                            AddVoxelFace(blockPos, Direction.Down, type, globalLogicIndex, outVertices, outIndices);
                        }
                        // Up (+Y)
                        if (IsFaceVisible(world, wx, wy + 1, wz))
                        {
                            AddVoxelFace(blockPos, Direction.Up, type, globalLogicIndex, outVertices, outIndices);
                        }
                        // North (-Z)
                        if (IsFaceVisible(world, wx, wy, wz - 1))
                        {
                            AddVoxelFace(blockPos, Direction.North, type, globalLogicIndex, outVertices, outIndices);
                        }
                        // South (+Z)
                        if (IsFaceVisible(world, wx, wy, wz + 1))
                        {
                            AddVoxelFace(blockPos, Direction.South, type, globalLogicIndex, outVertices, outIndices);
                        }
                    }
                }
            }
        }

        private static bool IsFaceVisible(WorldGrid world, int x, int y, int z)
        {
            if (!world.IsInsideWorld(x, y, z)) return true;
            ushort neighborId = world.GetBlockId(x, y, z);
            if (neighborId == (ushort)BlockType.Air) return true;
            if (BlockCategory.HasCustomGeoModel((BlockType)neighborId)) return true;
            return false;
        }

        private static void AddVoxelFace(
            Vector3 pos,
            byte direction,
            BlockType type,
            uint globalLogicIndex,
            List<VoxelVertex> vertices,
            List<uint> indices)
        {
            uint baseIdx = (uint)vertices.Count;
            var (uv0, uv1) = TextureAtlas.GetUVs(type, direction);

            Vector3 normal = Vector3.Zero;
            Vector3 v0 = Vector3.Zero, v1 = Vector3.Zero, v2 = Vector3.Zero, v3 = Vector3.Zero;

            switch (direction)
            {
                case Direction.West: // -X
                    normal = -Vector3.UnitX;
                    v0 = pos + new Vector3(0, 0, 0);
                    v1 = pos + new Vector3(0, 0, 1);
                    v2 = pos + new Vector3(0, 1, 1);
                    v3 = pos + new Vector3(0, 1, 0);
                    break;
                case Direction.East: // +X
                    normal = Vector3.UnitX;
                    v0 = pos + new Vector3(1, 0, 1);
                    v1 = pos + new Vector3(1, 0, 0);
                    v2 = pos + new Vector3(1, 1, 0);
                    v3 = pos + new Vector3(1, 1, 1);
                    break;
                case Direction.Down: // -Y
                    normal = -Vector3.UnitY;
                    v0 = pos + new Vector3(0, 0, 0);
                    v1 = pos + new Vector3(1, 0, 0);
                    v2 = pos + new Vector3(1, 0, 1);
                    v3 = pos + new Vector3(0, 0, 1);
                    break;
                case Direction.Up: // +Y
                    normal = Vector3.UnitY;
                    v0 = pos + new Vector3(0, 1, 1);
                    v1 = pos + new Vector3(1, 1, 1);
                    v2 = pos + new Vector3(1, 1, 0);
                    v3 = pos + new Vector3(0, 1, 0);
                    break;
                case Direction.North: // -Z
                    normal = -Vector3.UnitZ;
                    v0 = pos + new Vector3(1, 0, 0);
                    v1 = pos + new Vector3(0, 0, 0);
                    v2 = pos + new Vector3(0, 1, 0);
                    v3 = pos + new Vector3(1, 1, 0);
                    break;
                case Direction.South: // +Z
                    normal = Vector3.UnitZ;
                    v0 = pos + new Vector3(0, 0, 1);
                    v1 = pos + new Vector3(1, 0, 1);
                    v2 = pos + new Vector3(1, 1, 1);
                    v3 = pos + new Vector3(0, 1, 1);
                    break;
            }

            Vector3 colorTint = Vector3.One;

            vertices.Add(new VoxelVertex(v0, normal, new Vector2(uv0.X, uv1.Y), globalLogicIndex, colorTint));
            vertices.Add(new VoxelVertex(v1, normal, new Vector2(uv1.X, uv1.Y), globalLogicIndex, colorTint));
            vertices.Add(new VoxelVertex(v2, normal, new Vector2(uv1.X, uv0.Y), globalLogicIndex, colorTint));
            vertices.Add(new VoxelVertex(v3, normal, new Vector2(uv0.X, uv0.Y), globalLogicIndex, colorTint));

            indices.Add(baseIdx + 0);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 0);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        private static void AppendStaticGeoModel(
            int wx, int wy, int wz,
            uint globalLogicIndex,
            ParsedModel model,
            List<VoxelVertex> vertices,
            List<uint> indices)
        {
            uint baseIdx = (uint)vertices.Count;
            Vector3 offset = new Vector3(wx, wy, wz);

            for (int i = 0; i < model.StaticVertices.Count; i++)
            {
                var v = model.StaticVertices[i];
                vertices.Add(new VoxelVertex(
                    v.Position + offset,
                    v.Normal,
                    v.TexCoords,
                    globalLogicIndex,
                    v.ColorTint));
            }

            for (int i = 0; i < model.StaticIndices.Count; i++)
            {
                indices.Add(baseIdx + model.StaticIndices[i]);
            }
        }
    }
}
