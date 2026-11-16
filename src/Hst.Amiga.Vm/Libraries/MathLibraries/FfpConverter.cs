namespace Hst.Amiga.Vm.Libraries.MathLibraries;

using System;

/// <summary>
/// Converts between Motorola Fast Floating Point (FFP) and double. FFP is a 32-bit format with a 24-bit
/// normalized mantissa in bits 31-8, sign in bit 7 and excess-64 exponent in bits 6-0. Zero is all bits zero.
/// </summary>
public static class FfpConverter
{
    /// <summary>
    /// Largest positive FFP value.
    /// </summary>
    public const uint MaxValue = 0xFFFFFF7F;

    private const uint SignBit = 0x80;

    /// <summary>
    /// Convert FFP to double.
    /// </summary>
    public static double ToDouble(uint ffp)
    {
        var mantissa = ffp >> 8;
        if (mantissa == 0)
        {
            return 0;
        }

        var exponent = (int)(ffp & 0x7F) - 64;
        var value = mantissa * Math.Pow(2, exponent - 24);
        return (ffp & SignBit) != 0 ? -value : value;
    }

    /// <summary>
    /// Convert double to FFP. Values too large for FFP are converted to largest FFP value and values too small
    /// are converted to zero.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <param name="overflow">True, if value is too large for FFP or is not a number.</param>
    public static uint FromDouble(double value, out bool overflow)
    {
        overflow = false;
        if (double.IsNaN(value))
        {
            overflow = true;
            return 0;
        }

        var sign = value < 0 ? SignBit : 0;
        if (double.IsInfinity(value))
        {
            overflow = true;
            return MaxValue | sign;
        }

        var bits = BitConverter.DoubleToInt64Bits(value);
        var biasedExponent = (int)((bits >> 52) & 0x7FF);
        if (biasedExponent == 0)
        {
            // zero or subnormal, which is far below smallest ffp value
            return 0;
        }

        // value = 0.mantissa * 2^exponent with mantissa normalized to 0.5 <= m < 1
        var exponent = biasedExponent - 1022;
        var mantissa53 = (bits & 0xFFFFFFFFFFFFFL) | (1L << 52);
        var mantissa = (uint)((mantissa53 + (1L << 28)) >> 29);
        if (mantissa == 1U << 24)
        {
            mantissa >>= 1;
            exponent++;
        }

        var ffpExponent = exponent + 64;
        if (ffpExponent > 0x7F)
        {
            overflow = true;
            return MaxValue | sign;
        }

        if (ffpExponent < 0)
        {
            return 0;
        }

        return (mantissa << 8) | sign | (uint)ffpExponent;
    }
}
