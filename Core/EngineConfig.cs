using System.Numerics;

namespace BinaryTernaryCraft.Core
{
    public static class EngineConfig
    {
        // World Grid Dimensions (in Chunks: 16x16x16 voxels each)
        // Default: 16x16x12 chunks = 256 x 256 x 192 voxels
        public const int ChunkSize = 16;
        public const int ChunkVoxels = ChunkSize * ChunkSize * ChunkSize; // 4096

        public static int WorldChunksX { get; set; } = 16;
        public static int WorldChunksY { get; set; } = 12;
        public static int WorldChunksZ { get; set; } = 16;

        public static int TotalWorldVoxels => WorldChunksX * WorldChunksY * WorldChunksZ * ChunkVoxels;

        // Window & Graphics
        public static int WindowWidth { get; set; } = 1600;
        public static int WindowHeight { get; set; } = 900;
        public static string WindowTitle { get; set; } = "BinaryTernaryCraft - 3D Voxel Ternary & Kinetic Engine";
        public static bool VSync { get; set; } = false;

        // Simulation
        public static double TargetTPS { get; set; } = 1000.0;
        public static bool SimulationPaused { get; set; } = false;
        public static bool StepSingleTick { get; set; } = false;

        // Raycasting
        public static float MaxReachDistance { get; set; } = 10.0f;
    }
}
