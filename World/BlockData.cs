using System.Numerics;

namespace BinaryTernaryCraft.World
{
    public static class TritValue
    {
        public const sbyte Negative = -1; // Red (R)
        public const sbyte Zero = 0;      // Green (G - Neutral)
        public const sbyte Positive = 1;  // Blue (B)

        public static Vector3 GetColor(sbyte trit)
        {
            return trit switch
            {
                Negative => new Vector3(1.0f, 0.1f, 0.1f), // Glowing Red
                Positive => new Vector3(0.1f, 0.4f, 1.0f), // Glowing Blue
                _ => new Vector3(0.1f, 0.85f, 0.2f)        // Neutral Green
            };
        }
    }

    public static class Direction
    {
        public const byte West = 0;  // -X
        public const byte East = 1;  // +X
        public const byte Down = 2;  // -Y
        public const byte Up = 3;    // +Y
        public const byte North = 4; // -Z
        public const byte South = 5; // +Z

        public static readonly int[] OffsetX = { -1, 1, 0, 0, 0, 0 };
        public static readonly int[] OffsetY = { 0, 0, -1, 1, 0, 0 };
        public static readonly int[] OffsetZ = { 0, 0, 0, 0, -1, 1 };

        public static byte Opposite(byte dir)
        {
            return dir switch
            {
                West => East,
                East => West,
                Down => Up,
                Up => Down,
                North => South,
                South => North,
                _ => 0
            };
        }

        public static Vector3 GetAxisVector(byte dir)
        {
            return dir switch
            {
                West or East => Vector3.UnitX,
                Down or Up => Vector3.UnitY,
                _ => Vector3.UnitZ
            };
        }
    }
}
