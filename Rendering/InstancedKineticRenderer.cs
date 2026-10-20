using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using BinaryTernaryCraft.Kinetics;
using BinaryTernaryCraft.Models;
using BinaryTernaryCraft.World;
using Silk.NET.OpenGL;

namespace BinaryTernaryCraft.Rendering
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct KineticInstanceData
    {
        public Vector3 Position;       // 12 bytes
        public Vector3 Axis;           // 12 bytes (rotation axis, e.g. UnitZ)
        public Vector3 Pivot;          // 12 bytes
        public int NetworkId;          // 4 bytes
        public float GearRatio;        // 4 bytes

        public const uint SizeInBytes = 44;

        public KineticInstanceData(Vector3 position, Vector3 axis, Vector3 pivot, int networkId, float gearRatio)
        {
            Position = position;
            Axis = axis;
            Pivot = pivot;
            NetworkId = networkId;
            GearRatio = gearRatio;
        }
    }

    public class InstancedKineticRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Dictionary<BlockType, KineticMeshGroup> _meshGroups = new();

        private class KineticMeshGroup : IDisposable
        {
            public uint VAO;
            public uint VBO;
            public uint EBO;
            public uint InstanceVBO;
            public int IndexCount;
            public Vector3 Pivot;
            public List<KineticInstanceData> Instances = new();

            public void Dispose() { }
        }

        public InstancedKineticRenderer(GL gl, Dictionary<BlockType, ParsedModel> models)
        {
            _gl = gl;
            InitializeModels(models);
        }

        private unsafe void InitializeModels(Dictionary<BlockType, ParsedModel> models)
        {
            foreach (var kvp in models)
            {
                BlockType type = kvp.Key;
                ParsedModel model = kvp.Value;

                if (!model.HasKineticMesh) continue;

                var group = new KineticMeshGroup();
                group.IndexCount = model.KineticIndices.Count;
                group.Pivot = model.Pivot;

                group.VAO = _gl.GenVertexArray();
                group.VBO = _gl.GenBuffer();
                group.EBO = _gl.GenBuffer();
                group.InstanceVBO = _gl.GenBuffer();

                _gl.BindVertexArray(group.VAO);

                // Base Model Vertices
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, group.VBO);
                fixed (VoxelVertex* vPtr = model.KineticVertices.ToArray())
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(model.KineticVertices.Count * sizeof(VoxelVertex)), vPtr, BufferUsageARB.StaticDraw);
                }

                _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, group.EBO);
                fixed (uint* iPtr = model.KineticIndices.ToArray())
                {
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(model.KineticIndices.Count * sizeof(uint)), iPtr, BufferUsageARB.StaticDraw);
                }

                uint stride = VoxelVertex.SizeInBytes;
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
                _gl.EnableVertexAttribArray(3);
                _gl.VertexAttribIPointer(3, 1, VertexAttribIType.UnsignedInt, stride, (void*)32);
                _gl.EnableVertexAttribArray(4);
                _gl.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, (void*)36);

                // Instanced Buffer setup
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, group.InstanceVBO);
                uint instStride = KineticInstanceData.SizeInBytes;

                // 5: aInstancePos (vec3)
                _gl.EnableVertexAttribArray(5);
                _gl.VertexAttribPointer(5, 3, VertexAttribPointerType.Float, false, instStride, (void*)0);
                _gl.VertexAttribDivisor(5, 1);

                // 6: aInstanceAxis (vec3)
                _gl.EnableVertexAttribArray(6);
                _gl.VertexAttribPointer(6, 3, VertexAttribPointerType.Float, false, instStride, (void*)12);
                _gl.VertexAttribDivisor(6, 1);

                // 7: aInstancePivot (vec3)
                _gl.EnableVertexAttribArray(7);
                _gl.VertexAttribPointer(7, 3, VertexAttribPointerType.Float, false, instStride, (void*)24);
                _gl.VertexAttribDivisor(7, 1);

                // 8: aInstanceNetworkId (int)
                _gl.EnableVertexAttribArray(8);
                _gl.VertexAttribIPointer(8, 1, VertexAttribIType.Int, instStride, (void*)36);
                _gl.VertexAttribDivisor(8, 1);

                // 9: aInstanceGearRatio (float)
                _gl.EnableVertexAttribArray(9);
                _gl.VertexAttribPointer(9, 1, VertexAttribPointerType.Float, false, instStride, (void*)40);
                _gl.VertexAttribDivisor(9, 1);

                _gl.BindVertexArray(0);

                _meshGroups[type] = group;
            }
        }

        public void CollectInstances(WorldGrid world, KineticNetworkManager? kineticMgr = null)
        {
            foreach (var group in _meshGroups.Values)
            {
                group.Instances.Clear();
            }

            for (int cx = 0; cx < world.ChunksX; cx++)
            {
                for (int cy = 0; cy < world.ChunksY; cy++)
                {
                    for (int cz = 0; cz < world.ChunksZ; cz++)
                    {
                        var chunk = world.GetChunk(cx, cy, cz);
                        if (chunk == null || chunk.IsEmpty) continue;

                        int startX = cx * Chunk.Size;
                        int startY = cy * Chunk.Size;
                        int startZ = cz * Chunk.Size;

                        for (int ly = 0; ly < Chunk.Size; ly++)
                        {
                            int wy = startY + ly;
                            for (int lz = 0; lz < Chunk.Size; lz++)
                            {
                                int wz = startZ + lz;
                                for (int lx = 0; lx < Chunk.Size; lx++)
                                {
                                    int wx = startX + lx;
                                    ushort id = chunk.GetBlockId(lx, ly, lz);
                                    BlockType type = (BlockType)id;

                                    if (_meshGroups.TryGetValue(type, out var group))
                                    {
                                        byte meta = chunk.BlockMeta[Chunk.GetIndex(lx, ly, lz)];
                                        int netId = chunk.NetworkId[Chunk.GetIndex(lx, ly, lz)];
                                        Vector3 axis = Direction.GetAxisVector(meta);
                                        Vector3 pos = new Vector3(wx, wy, wz);
                                        float ratio = 1.0f;

                                        if (type == BlockType.TernaryPiston)
                                        {
                                            int extension = chunk.BlockTimerConfig[Chunk.GetIndex(lx, ly, lz)];
                                            Vector3 forward = new Vector3(Direction.OffsetX[meta], Direction.OffsetY[meta], Direction.OffsetZ[meta]);
                                            pos += forward * extension;
                                            ratio = 0.0f;
                                            netId = 0;
                                        }
                                        else if (kineticMgr != null)
                                        {
                                            ratio = kineticMgr.GetGearRatio(wx, wy, wz);
                                        }

                                        group.Instances.Add(new KineticInstanceData(
                                            pos,
                                            axis,
                                            group.Pivot,
                                            netId,
                                            ratio));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public unsafe void RenderAll()
        {
            foreach (var group in _meshGroups.Values)
            {
                if (group.Instances.Count == 0 || group.IndexCount == 0) continue;

                _gl.BindVertexArray(group.VAO);

                // Upload instance data to GPU
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, group.InstanceVBO);
                fixed (KineticInstanceData* instPtr = group.Instances.ToArray())
                {
                    _gl.BufferData(
                        BufferTargetARB.ArrayBuffer,
                        (nuint)(group.Instances.Count * sizeof(KineticInstanceData)),
                        instPtr,
                        BufferUsageARB.DynamicDraw);
                }

                _gl.DrawElementsInstanced(
                    PrimitiveType.Triangles,
                    (uint)group.IndexCount,
                    DrawElementsType.UnsignedInt,
                    (void*)0,
                    (uint)group.Instances.Count);

                _gl.BindVertexArray(0);
            }
        }

        public void Dispose()
        {
            foreach (var group in _meshGroups.Values)
            {
                if (group.VAO != 0) _gl.DeleteVertexArray(group.VAO);
                if (group.VBO != 0) _gl.DeleteBuffer(group.VBO);
                if (group.EBO != 0) _gl.DeleteBuffer(group.EBO);
                if (group.InstanceVBO != 0) _gl.DeleteBuffer(group.InstanceVBO);
            }
            _meshGroups.Clear();
        }
    }
}
