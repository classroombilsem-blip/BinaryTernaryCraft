using System.Numerics;
using System.Runtime.InteropServices;

namespace BinaryTernaryCraft.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VoxelVertex
    {
        public Vector3 Position;          // 12 bytes
        public Vector3 Normal;            // 12 bytes
        public Vector2 TexCoords;         // 8 bytes
        public uint GlobalLogicIndex;     // 4 bytes: for SSBO dynamic coloring
        public Vector3 ColorTint;         // 12 bytes: Base RGB color / emissive flag

        public const uint SizeInBytes = 48;

        public VoxelVertex(Vector3 position, Vector3 normal, Vector2 texCoords, uint globalLogicIndex, Vector3 colorTint)
        {
            Position = position;
            Normal = normal;
            TexCoords = texCoords;
            GlobalLogicIndex = globalLogicIndex;
            ColorTint = colorTint;
        }
    }

    public class ParsedModel
    {
        public string ModelName { get; set; } = string.Empty;
        public Vector3 Pivot { get; set; } = new Vector3(0.5f, 0.5f, 0.5f);

        // Vertices and Indices for Static part (baked into chunk mesh)
        public List<VoxelVertex> StaticVertices { get; } = new();
        public List<uint> StaticIndices { get; } = new();

        // Vertices and Indices for Kinetic part (GPU instanced drawing)
        public List<VoxelVertex> KineticVertices { get; } = new();
        public List<uint> KineticIndices { get; } = new();

        public bool HasStaticMesh => StaticVertices.Count > 0;
        public bool HasKineticMesh => KineticVertices.Count > 0;
    }
}
