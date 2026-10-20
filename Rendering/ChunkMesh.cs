using System;
using System.Collections.Generic;
using BinaryTernaryCraft.Models;
using Silk.NET.OpenGL;

namespace BinaryTernaryCraft.Rendering
{
    public class ChunkMesh : IDisposable
    {
        private readonly GL _gl;
        public uint VAO { get; private set; }
        public uint VBO { get; private set; }
        public uint EBO { get; private set; }
        public int IndexCount { get; private set; }

        public ChunkMesh(GL gl)
        {
            _gl = gl;
            VAO = _gl.GenVertexArray();
            VBO = _gl.GenBuffer();
            EBO = _gl.GenBuffer();

            SetupVertexAttributes();
        }

        private unsafe void SetupVertexAttributes()
        {
            _gl.BindVertexArray(VAO);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, VBO);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, EBO);

            uint stride = VoxelVertex.SizeInBytes; // 48 bytes

            // 0: Position (vec3)
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

            // 1: Normal (vec3)
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);

            // 2: TexCoords (vec2)
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)24);

            // 3: GlobalLogicIndex (uint) - Use VertexAttribIPointer for integer attribute!
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribIPointer(3, 1, VertexAttribIType.UnsignedInt, stride, (void*)32);

            // 4: ColorTint (vec3)
            _gl.EnableVertexAttribArray(4);
            _gl.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, (void*)36);

            _gl.BindVertexArray(0);
        }

        public unsafe void UploadData(List<VoxelVertex> vertices, List<uint> indices)
        {
            IndexCount = indices.Count;
            if (IndexCount == 0) return;

            _gl.BindVertexArray(VAO);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, VBO);
            fixed (VoxelVertex* vPtr = vertices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(VoxelVertex)), vPtr, BufferUsageARB.StaticDraw);
            }

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, EBO);
            fixed (uint* iPtr = indices.ToArray())
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), iPtr, BufferUsageARB.StaticDraw);
            }

            _gl.BindVertexArray(0);
        }

        public unsafe void Render()
        {
            if (IndexCount == 0) return;

            _gl.BindVertexArray(VAO);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)IndexCount, DrawElementsType.UnsignedInt, (void*)0);
            _gl.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (VAO != 0)
            {
                _gl.DeleteVertexArray(VAO);
                VAO = 0;
            }
            if (VBO != 0)
            {
                _gl.DeleteBuffer(VBO);
                VBO = 0;
            }
            if (EBO != 0)
            {
                _gl.DeleteBuffer(EBO);
                EBO = 0;
            }
        }
    }
}
