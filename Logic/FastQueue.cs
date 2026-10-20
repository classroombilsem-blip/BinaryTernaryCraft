using System;
using System.Runtime.CompilerServices;

namespace BinaryTernaryCraft.Logic
{
    /// <summary>
    /// High-performance, zero-allocation pre-allocated queue for voxel coordinates.
    /// Encodes (X, Y, Z) into a compact 32-bit int:
    /// X: 10 bits (0..1023), Y: 10 bits (0..1023), Z: 10 bits (0..1023).
    /// </summary>
    public class FastQueue
    {
        private int[] _elements;
        public int Count { get; private set; }
        public readonly int Capacity;

        public FastQueue(int capacity)
        {
            Capacity = capacity;
            _elements = new int[capacity];
            Count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PackCoord(int x, int y, int z)
        {
            return (x & 0x3FF) | ((y & 0x3FF) << 10) | ((z & 0x3FF) << 20);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UnpackCoord(int packed, out int x, out int y, out int z)
        {
            x = packed & 0x3FF;
            y = (packed >> 10) & 0x3FF;
            z = (packed >> 20) & 0x3FF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(int packed)
        {
            if (Count < Capacity)
            {
                _elements[Count++] = packed;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Get(int index)
        {
            return _elements[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }
    }
}
