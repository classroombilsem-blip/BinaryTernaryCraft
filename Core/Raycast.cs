using System;
using System.Numerics;
using BinaryTernaryCraft.World;

namespace BinaryTernaryCraft.Core
{
    public struct RaycastHit
    {
        public bool HasHit;
        public int BlockX;
        public int BlockY;
        public int BlockZ;
        public int NormalX;
        public int NormalY;
        public int NormalZ;
        public ushort BlockId;
        public float Distance;
    }

    public static class Raycast
    {
        /// <summary>
        /// Fast Voxel Traversal Algorithm (Amanatides & Woo) for 3D grids
        /// </summary>
        public static RaycastHit Cast(WorldGrid world, Vector3 origin, Vector3 direction, float maxDistance)
        {
            RaycastHit result = default;
            result.HasHit = false;

            if (direction == Vector3.Zero) return result;
            direction = Vector3.Normalize(direction);

            int x = (int)MathF.Floor(origin.X);
            int y = (int)MathF.Floor(origin.Y);
            int z = (int)MathF.Floor(origin.Z);

            int stepX = direction.X > 0 ? 1 : (direction.X < 0 ? -1 : 0);
            int stepY = direction.Y > 0 ? 1 : (direction.Y < 0 ? -1 : 0);
            int stepZ = direction.Z > 0 ? 1 : (direction.Z < 0 ? -1 : 0);

            float tDeltaX = stepX != 0 ? MathF.Abs(1.0f / direction.X) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? MathF.Abs(1.0f / direction.Y) : float.PositiveInfinity;
            float tDeltaZ = stepZ != 0 ? MathF.Abs(1.0f / direction.Z) : float.PositiveInfinity;

            float tMaxX = stepX > 0 ? (MathF.Floor(origin.X) + 1.0f - origin.X) * tDeltaX : (origin.X - MathF.Floor(origin.X)) * tDeltaX;
            float tMaxY = stepY > 0 ? (MathF.Floor(origin.Y) + 1.0f - origin.Y) * tDeltaY : (origin.Y - MathF.Floor(origin.Y)) * tDeltaY;
            float tMaxZ = stepZ > 0 ? (MathF.Floor(origin.Z) + 1.0f - origin.Z) * tDeltaZ : (origin.Z - MathF.Floor(origin.Z)) * tDeltaZ;

            int normalX = 0, normalY = 0, normalZ = 0;
            float dist = 0.0f;

            while (dist <= maxDistance)
            {
                if (world.IsInsideWorld(x, y, z))
                {
                    ushort id = world.GetBlockId(x, y, z);
                    if (id != (ushort)BlockType.Air)
                    {
                        result.HasHit = true;
                        result.BlockX = x;
                        result.BlockY = y;
                        result.BlockZ = z;
                        result.NormalX = normalX;
                        result.NormalY = normalY;
                        result.NormalZ = normalZ;
                        result.BlockId = id;
                        result.Distance = dist;
                        return result;
                    }
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        dist = tMaxX;
                        x += stepX;
                        tMaxX += tDeltaX;
                        normalX = -stepX;
                        normalY = 0;
                        normalZ = 0;
                    }
                    else
                    {
                        dist = tMaxZ;
                        z += stepZ;
                        tMaxZ += tDeltaZ;
                        normalX = 0;
                        normalY = 0;
                        normalZ = -stepZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        dist = tMaxY;
                        y += stepY;
                        tMaxY += tDeltaY;
                        normalX = 0;
                        normalY = -stepY;
                        normalZ = 0;
                    }
                    else
                    {
                        dist = tMaxZ;
                        z += stepZ;
                        tMaxZ += tDeltaZ;
                        normalX = 0;
                        normalY = 0;
                        normalZ = -stepZ;
                    }
                }
            }

            return result;
        }
    }
}
