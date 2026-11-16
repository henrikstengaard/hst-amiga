namespace Hst.Amiga.Tests.VmTests;

using System;
using Vm;
using Vm.Libraries.Exec;
using Vm.Libraries.MathLibraries;
using Xunit;

public class GivenMathLibraries
{
    private readonly VmContext context;
    private readonly ExecLibrary exec;

    public GivenMathLibraries()
    {
        context = new VmContext(new VmOptions { Log = _ => { } });
        exec = new ExecLibrary(context);
        exec.Install();
        context.AddLibrary(exec);
        var stack = context.Allocator.Allocate(4096);
        context.Cpu.A[7] = stack + 4096;
    }

    private MathLibrary Install(MathLibrary library)
    {
        library.Install();
        context.AddLibrary(library);
        return library;
    }

    private MathLibrary Bas(MathFormat format) => Install(new MathBasLibrary(context, exec, format));
    private MathLibrary Trans(MathFormat format) => Install(new MathTransLibrary(context, exec, format));

    private uint Call(MathLibrary library, string function, uint d0 = 0, uint d1 = 0, uint d2 = 0, uint d3 = 0,
        uint a0 = 0)
    {
        var cpu = context.Cpu;
        cpu.D[0] = d0;
        cpu.D[1] = d1;
        cpu.D[2] = d2;
        cpu.D[3] = d3;
        cpu.A[0] = a0;
        return context.CallSubroutine((uint)(library.BaseAddress + library.GetOffset(function)));
    }

    private static uint Ffp(double value) => FfpConverter.FromDouble(value, out _);
    private static uint Single(float value) => MathLibrary.FromSingle(value);
    private static (uint High, uint Low) Double(double value)
    {
        var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        return ((uint)(bits >> 32), (uint)bits);
    }

    private double ResultDouble() => MathLibrary.ToDouble(context.Cpu.D[0], context.Cpu.D[1]);

    [Theory]
    [InlineData(1.0, 0x80000041U)]
    [InlineData(-1.0, 0x800000C1U)]
    [InlineData(0.5, 0x80000040U)]
    [InlineData(10.0, 0xA0000044U)]
    [InlineData(0.0, 0x00000000U)]
    public void When_ConvertingFfp_Then_ValueIsConvertedBothWays(double value, uint ffp)
    {
        Assert.Equal(ffp, FfpConverter.FromDouble(value, out var overflow));
        Assert.False(overflow);
        Assert.Equal(value, FfpConverter.ToDouble(ffp));
    }

    [Fact]
    public void When_ConvertingTooLargeValueToFfp_Then_MaxValueIsReturnedWithOverflow()
    {
        Assert.Equal(FfpConverter.MaxValue, FfpConverter.FromDouble(1e30, out var overflow));
        Assert.True(overflow);
        Assert.Equal(FfpConverter.MaxValue | 0x80, FfpConverter.FromDouble(double.NegativeInfinity, out _));
        Assert.Equal(0U, FfpConverter.FromDouble(1e-30, out overflow));
        Assert.False(overflow);
    }

    [Fact]
    public void When_CallingFfpArithmetic_Then_ResultsAreComputedWithD0AsFirstOperand()
    {
        var ffp = Bas(MathFormat.Ffp);

        Assert.Equal(4.0, FfpConverter.ToDouble(Call(ffp, "SPAdd", Ffp(1.5), Ffp(2.5))));
        Assert.Equal(7.0, FfpConverter.ToDouble(Call(ffp, "SPSub", Ffp(10), Ffp(3))));
        Assert.Equal(-6.0, FfpConverter.ToDouble(Call(ffp, "SPMul", Ffp(2), Ffp(-3))));
        Assert.Equal(2.5, FfpConverter.ToDouble(Call(ffp, "SPDiv", Ffp(10), Ffp(4))));
        Assert.Equal(42U, Call(ffp, "SPFix", Ffp(42.9)));
        Assert.Equal(unchecked((uint)-42), Call(ffp, "SPFix", Ffp(-42.9)));
        Assert.Equal(-7.0, FfpConverter.ToDouble(Call(ffp, "SPFlt", unchecked((uint)-7))));
        Assert.Equal(3.0, FfpConverter.ToDouble(Call(ffp, "SPAbs", Ffp(-3))));
        Assert.Equal(-3.0, FfpConverter.ToDouble(Call(ffp, "SPNeg", Ffp(3))));
        Assert.Equal(2.0, FfpConverter.ToDouble(Call(ffp, "SPFloor", Ffp(2.5))));
        Assert.Equal(3.0, FfpConverter.ToDouble(Call(ffp, "SPCeil", Ffp(2.5))));
    }

    [Fact]
    public void When_CallingFfpCompareAndTest_Then_D1IsComparedToD0AndConditionCodesAreSet()
    {
        var ffp = Bas(MathFormat.Ffp);

        Assert.Equal(1U, Call(ffp, "SPCmp", Ffp(1), Ffp(2)));
        Assert.False(context.Cpu.N);
        Assert.False(context.Cpu.Z);

        Assert.Equal(unchecked((uint)-1), Call(ffp, "SPCmp", Ffp(2), Ffp(1)));
        Assert.True(context.Cpu.N);

        Assert.Equal(0U, Call(ffp, "SPCmp", Ffp(2), Ffp(2)));
        Assert.True(context.Cpu.Z);

        Assert.Equal(unchecked((uint)-1), Call(ffp, "SPTst", 0, Ffp(-5)));
        Assert.True(context.Cpu.N);
        Assert.Equal(0U, Call(ffp, "SPTst", Ffp(5), 0));
        Assert.True(context.Cpu.Z);
    }

    [Fact]
    public void When_CallingIeeeSingleFunctions_Then_ResultsAreComputed()
    {
        var bas = Bas(MathFormat.IeeeSingle);
        var trans = Trans(MathFormat.IeeeSingle);

        Assert.Equal(Single(0.3f), Call(bas, "IEEESPAdd", Single(0.1f), Single(0.2f)));
        Assert.Equal(Single(-1.5f), Call(bas, "IEEESPSub", Single(1f), Single(2.5f)));
        Assert.Equal(1U, Call(bas, "IEEESPCmp", Single(3f), Single(2f)));
        Assert.Equal(unchecked((uint)-1), Call(bas, "IEEESPTst", Single(-0.5f)));
        Assert.Equal(Single(3f), Call(trans, "IEEESPSqrt", Single(9f)));
        Assert.Equal(Single(8f), Call(trans, "IEEESPPow", Single(2f), Single(3f)));
    }

    [Fact]
    public void When_CallingIeeeDoubleFunctions_Then_RegisterPairsAreUsed()
    {
        var bas = Bas(MathFormat.IeeeDouble);
        var trans = Trans(MathFormat.IeeeDouble);
        var left = Double(10.0);
        var right = Double(4.0);

        Call(bas, "IEEEDPDiv", left.High, left.Low, right.High, right.Low);
        Assert.Equal(2.5, ResultDouble());

        Call(bas, "IEEEDPSub", left.High, left.Low, right.High, right.Low);
        Assert.Equal(6.0, ResultDouble());

        Assert.Equal(unchecked((uint)-1), Call(bas, "IEEEDPCmp", right.High, right.Low, left.High, left.Low));
        Assert.True(context.Cpu.N);

        var big = Double(3e9);
        Assert.Equal((uint)int.MaxValue, Call(bas, "IEEEDPFix", big.High, big.Low));

        Call(bas, "IEEEDPFlt", unchecked((uint)-3));
        Assert.Equal(-3.0, ResultDouble());

        Call(trans, "IEEEDPPow", left.High, left.Low, Double(2).High, Double(2).Low);
        Assert.Equal(100.0, ResultDouble());

        var half = Double(0.5);
        Assert.Equal(Single(0.5f), Call(trans, "IEEEDPTieee", half.High, half.Low));
        Call(trans, "IEEEDPFieee", Single(0.25f));
        Assert.Equal(0.25, ResultDouble());
    }

    [Fact]
    public void When_CallingSincos_Then_SineIsReturnedAndCosineIsWrittenToPointer()
    {
        var doubleTrans = Trans(MathFormat.IeeeDouble);
        var ffpTrans = Trans(MathFormat.Ffp);
        var pointer = context.Allocator.Allocate(8);
        var zero = Double(0);

        Call(doubleTrans, "IEEEDPSincos", zero.High, zero.Low, a0: pointer);
        Assert.Equal(0.0, ResultDouble());
        Assert.Equal(1.0, MathLibrary.ToDouble(context.Ram.ReadLong(pointer), context.Ram.ReadLong(pointer + 4)));

        var sine = Call(ffpTrans, "SPSincos", Ffp(Math.PI / 2), pointer);
        Assert.Equal(1.0, FfpConverter.ToDouble(sine), 6);
        Assert.Equal(0.0, FfpConverter.ToDouble(context.Ram.ReadLong(pointer)), 6);
    }

    [Fact]
    public void When_ConvertingFfpToAndFromIeee_Then_ValueIsKept()
    {
        var trans = Trans(MathFormat.Ffp);

        Assert.Equal(Single(-12.5f), Call(trans, "SPTieee", Ffp(-12.5)));
        Assert.Equal(Ffp(0.75), Call(trans, "SPFieee", Single(0.75f)));
    }

    [Theory]
    [InlineData("mathffp.library")]
    [InlineData("mathtrans.library")]
    [InlineData("mathieeesingbas.library")]
    [InlineData("mathieeesingtrans.library")]
    [InlineData("mathieeedoubbas.library")]
    [InlineData("mathieeedoubtrans.library")]
    public void When_OpeningMathLibrary_Then_LibraryBaseIsReturned(string name)
    {
        foreach (var format in new[] { MathFormat.Ffp, MathFormat.IeeeSingle, MathFormat.IeeeDouble })
        {
            Bas(format);
            Trans(format);
        }

        var library = context.FindLibrary(name);
        Assert.NotNull(library);
        Assert.Equal(library.BaseAddress, exec.OpenLibrary(name, 37));
    }
}
