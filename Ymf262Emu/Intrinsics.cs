using System;
using System.Runtime.CompilerServices;

namespace Ymf262Emu
{
    internal static class Intrinsics
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExtractBits(uint value, byte start, byte length, uint mask)
        {
            if (System.Runtime.Intrinsics.X86.Bmi1.IsSupported)
                return System.Runtime.Intrinsics.X86.Bmi1.BitFieldExtract(value, start, length);
            else
                return (value & mask) >> start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Log2(double x) => Math.Log2(x);
    }
}
