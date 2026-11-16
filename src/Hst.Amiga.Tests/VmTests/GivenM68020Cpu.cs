namespace Hst.Amiga.Tests.VmTests;

using Vm.Processor;
using Xunit;
using static CpuTestHelper;

public class GivenM68020Cpu
{
    [Fact]
    public void When_MultiplyingLongWithOverflow_Then_OverflowIsSet()
    {
        // mulu.l d1,d0
        var cpu = CreateCpu(CpuType.M68020, 0x4C01, 0x0000);
        cpu.D[0] = 0x10000;
        cpu.D[1] = 0x10000;

        cpu.Step();

        Assert.Equal(0u, cpu.D[0]);
        Assert.True(cpu.V);
    }

    [Fact]
    public void When_MultiplyingLongWith64BitResult_Then_HighAndLowRegistersAreSet()
    {
        // mulu.l d1,d3:d0
        var cpu = CreateCpu(CpuType.M68020, 0x4C01, 0x0403);
        cpu.D[0] = 0x10000;
        cpu.D[1] = 0x10000;

        cpu.Step();

        Assert.Equal(0u, cpu.D[0]);
        Assert.Equal(1u, cpu.D[3]);
        Assert.False(cpu.V);
    }

    [Fact]
    public void When_DividingSignedLong_Then_QuotientIsStored()
    {
        // divs.l d1,d0
        var cpu = CreateCpu(CpuType.M68020, 0x4C41, 0x0800);
        cpu.D[0] = unchecked((uint)-100);
        cpu.D[1] = 7;

        cpu.Step();

        Assert.Equal(unchecked((uint)-14), cpu.D[0]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_DividingUnsignedLongWithRemainderRegister_Then_QuotientAndRemainderAreStored()
    {
        // divul.l d1,d2:d0
        var cpu = CreateCpu(CpuType.M68020, 0x4C41, 0x0002);
        cpu.D[0] = 100;
        cpu.D[1] = 7;

        cpu.Step();

        Assert.Equal(14u, cpu.D[0]);
        Assert.Equal(2u, cpu.D[2]);
    }

    [Fact]
    public void When_ExtendingByteToLong_Then_ValueIsSignExtended()
    {
        // extb.l d0
        var cpu = CreateCpu(CpuType.M68020, 0x49C0);
        cpu.D[0] = 0x12345680;

        cpu.Step();

        Assert.Equal(0xFFFFFF80u, cpu.D[0]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_ExtractingBitFieldFromRegister_Then_FieldIsExtracted()
    {
        // bfextu d0{4:8},d1
        var cpu = CreateCpu(CpuType.M68020, 0xE9C0, 0x1108);
        cpu.D[0] = 0x12345678;

        cpu.Step();

        Assert.Equal(0x23u, cpu.D[1]);
    }

    [Fact]
    public void When_InsertingBitFieldInRegister_Then_FieldIsInserted()
    {
        // bfins d1,d0{8:4}
        var cpu = CreateCpu(CpuType.M68020, 0xEFC0, 0x1204);
        cpu.D[0] = 0;
        cpu.D[1] = 0xF;

        cpu.Step();

        Assert.Equal(0x00F00000u, cpu.D[0]);
    }

    [Fact]
    public void When_ExtractingSignedBitFieldFromMemory_Then_FieldIsSignExtended()
    {
        // bfexts (a0){0:4},d1
        var cpu = CreateCpu(CpuType.M68020, 0xEBD0, 0x1004);
        cpu.A[0] = 0x3000;
        cpu.Ram.WriteByte(0x3000, 0xF0);

        cpu.Step();

        Assert.Equal(0xFFFFFFFFu, cpu.D[1]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_SettingBitFieldSpanningBytesInMemory_Then_OnlyFieldBitsAreChanged()
    {
        // bfset (a0){6:4}
        var cpu = CreateCpu(CpuType.M68020, 0xEED0, 0x0184);
        cpu.A[0] = 0x3000;

        cpu.Step();

        Assert.Equal(0x03, cpu.Ram.ReadByte(0x3000));
        Assert.Equal(0xC0, cpu.Ram.ReadByte(0x3001));
        Assert.Equal(0x00, cpu.Ram.ReadByte(0x3002));
        Assert.True(cpu.Z);
    }

    [Fact]
    public void When_FindingFirstOneInBitField_Then_OffsetOfFirstSetBitIsReturned()
    {
        // bfffo d0{0:32},d1
        var cpu = CreateCpu(CpuType.M68020, 0xEDC0, 0x1000);
        cpu.D[0] = 0x00010000;

        cpu.Step();

        Assert.Equal(15u, cpu.D[1]);
    }

    [Fact]
    public void When_UsingMemoryIndirectPreIndexedAddressing_Then_EffectiveAddressIsCalculated()
    {
        // move.l ([4,a0,d1.l*4],8),d0
        var cpu = CreateCpu(CpuType.M68020, 0x2030, 0x1D22, 0x0004, 0x0008);
        cpu.A[0] = 0x2000;
        cpu.D[1] = 1;
        cpu.Ram.WriteLong(0x2008, 0x3000);
        cpu.Ram.WriteLong(0x3008, 0xCAFEBABE);

        cpu.Step();

        Assert.Equal(0xCAFEBABEu, cpu.D[0]);
    }

    [Fact]
    public void When_UsingScaledIndexAddressing_Then_IndexIsScaled()
    {
        // move.l 0(a0,d1.l*8),d0
        var cpu = CreateCpu(CpuType.M68020, 0x2030, 0x1E00);
        cpu.A[0] = 0x2000;
        cpu.D[1] = 2;
        cpu.Ram.WriteLong(0x2010, 0x12345678);

        cpu.Step();

        Assert.Equal(0x12345678u, cpu.D[0]);
    }

    [Theory]
    [InlineData(5u, 0x22222222u, 5u, true)]
    [InlineData(6u, 6u, 5u, false)]
    public void When_ComparingAndSwapping_Then_MemoryIsUpdatedIfEqual(uint memoryValue, uint expectedMemory,
        uint expectedCompare, bool expectedZero)
    {
        // cas.l d1,d2,(a0)
        var cpu = CreateCpu(CpuType.M68020, 0x0ED0, 0x0081);
        cpu.A[0] = 0x3000;
        cpu.D[1] = 5;
        cpu.D[2] = 0x22222222;
        cpu.Ram.WriteLong(0x3000, memoryValue);

        cpu.Step();

        Assert.Equal(expectedMemory, cpu.Ram.ReadLong(0x3000));
        Assert.Equal(expectedZero ? expectedCompare : memoryValue, cpu.D[1]);
        Assert.Equal(expectedZero, cpu.Z);
    }

    [Fact]
    public void When_CheckingRegisterWithinBounds_Then_NoExceptionIsRaised()
    {
        // chk2.l (a0),d0
        var cpu = CreateCpu(CpuType.M68020, 0x04D0, 0x0800);
        cpu.A[0] = 0x3000;
        cpu.Ram.WriteLong(0x3000, 10);
        cpu.Ram.WriteLong(0x3004, 20);
        cpu.D[0] = 15;

        cpu.Step();

        Assert.False(cpu.C);
        Assert.False(cpu.Z);
    }

    [Fact]
    public void When_CheckingRegisterOutOfBounds_Then_ChkExceptionIsRaised()
    {
        // chk2.l (a0),d0
        var cpu = CreateCpu(CpuType.M68020, 0x04D0, 0x0800);
        cpu.A[0] = 0x3000;
        cpu.Ram.WriteLong(0x3000, 10);
        cpu.Ram.WriteLong(0x3004, 20);
        cpu.D[0] = 25;

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.Chk, exception.Vector);
    }

    [Fact]
    public void When_ExecutingFpuInstruction_Then_LineFExceptionIsRaised()
    {
        var cpu = CreateCpu(CpuType.M68020, 0xF200, 0x0000);

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.LineF, exception.Vector);
    }

    [Fact]
    public void When_BranchingWithLongDisplacement_Then_PcIsSet()
    {
        // bra.l *+$10002
        var cpu = CreateCpu(CpuType.M68020, 0x60FF, 0x0001, 0x0000);

        cpu.Step();

        Assert.Equal(CodeAddress + 2 + 0x10000, cpu.PC);
    }

    [Fact]
    public void When_MovingToAndFromVectorBaseRegister_Then_ValueIsSet()
    {
        // movec d0,vbr ; movec vbr,d1
        var cpu = CreateCpu(CpuType.M68020, 0x4E7B, 0x0801, 0x4E7A, 0x1801);
        cpu.D[0] = 0x8000;

        cpu.Step();
        cpu.Step();

        Assert.Equal(0x8000u, cpu.Vbr);
        Assert.Equal(0x8000u, cpu.D[1]);
    }
}
