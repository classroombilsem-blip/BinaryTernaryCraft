using System;
using System.Runtime.CompilerServices;

namespace BinaryTernaryCraft.Logic
{
    public static class TernaryGateLogic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte ClampTrit(int value)
        {
            if (value > 1) return 1;
            if (value < -1) return -1;
            return (sbyte)value;
        }

        // 1. Inverter (NOT): -1 -> +1, 0 -> 0, +1 -> -1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Not(sbyte inA)
        {
            return (sbyte)(-inA);
        }

        // 2. CYCLE (+1 Shift): -1 -> 0, 0 -> +1, +1 -> -1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Cycle(sbyte inA)
        {
            return inA switch
            {
                -1 => 0,
                0 => 1,
                _ => -1
            };
        }

        // 3. ANTI-CYCLE (-1 Shift): -1 -> +1, 0 -> -1, +1 -> 0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte AntiCycle(sbyte inA)
        {
            return inA switch
            {
                -1 => 1,
                0 => -1,
                _ => 0
            };
        }

        // 4. MIN (Ternary AND)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Min(sbyte inA, sbyte inB)
        {
            return inA < inB ? inA : inB;
        }

        // 5. MAX (Ternary OR)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Max(sbyte inA, sbyte inB)
        {
            return inA > inB ? inA : inB;
        }

        // 6. SUM (Balanced Ternary Half-Adder Modular Sum)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Sum(sbyte inA, sbyte inB)
        {
            int s = inA + inB;
            return s switch
            {
                -2 => 1,
                -1 => -1,
                0 => 0,
                1 => 1,
                2 => -1,
                _ => 0
            };
        }

        // 7. CONSENSUS (Carry for Balanced Ternary Adder)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte Consensus(sbyte inA, sbyte inB)
        {
            if (inA == 1 && inB == 1) return 1;
            if (inA == -1 && inB == -1) return -1;
            return 0;
        }
    }
}
