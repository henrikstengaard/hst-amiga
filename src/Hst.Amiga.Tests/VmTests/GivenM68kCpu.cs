namespace Hst.Amiga.Tests.VmTests;

using Vm.Processor;
using Xunit;
using static CpuTestHelper;

public class GivenM68kCpu
{
    [Theory]
    [InlineData(CpuType.M68000)]
    [InlineData(CpuType.M68020)]
    public void When_ExecutingMoveq_Then_DataRegisterIsSignExtendedAndFlagsAreSet(CpuType type)
    {
        // moveq #-1,d0
        var cpu = CreateCpu(type, 0x70FF);

        cpu.Step();

        Assert.Equal(0xFFFFFFFFu, cpu.D[0]);
        Assert.True(cpu.N);
        Assert.False(cpu.Z);
        Assert.False(cpu.V);
        Assert.False(cpu.C);
        Assert.Equal(CodeAddress + 2, cpu.PC);
    }

    [Fact]
    public void When_MovingByteToDataRegister_Then_UpperBytesAreUnchanged()
    {
        // move.b d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x1001);
        cpu.D[0] = 0x12345678;
        cpu.D[1] = 0x000000AB;

        cpu.Step();

        Assert.Equal(0x123456ABu, cpu.D[0]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_MovingWordToAddressRegister_Then_ValueIsSignExtendedAndFlagsAreUnchanged()
    {
        // movea.w d0,a0
        var cpu = CreateCpu(CpuType.M68000, 0x3040);
        cpu.D[0] = 0x00008000;
        cpu.Z = true;

        cpu.Step();

        Assert.Equal(0xFFFF8000u, cpu.A[0]);
        Assert.True(cpu.Z);
    }

    [Fact]
    public void When_AddingLongWithOverflow_Then_OverflowAndNegativeAreSet()
    {
        // add.l d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0xD081);
        cpu.D[0] = 0x7FFFFFFF;
        cpu.D[1] = 1;

        cpu.Step();

        Assert.Equal(0x80000000u, cpu.D[0]);
        Assert.True(cpu.V);
        Assert.True(cpu.N);
        Assert.False(cpu.C);
        Assert.False(cpu.X);
    }

    [Fact]
    public void When_AddingByteWithCarry_Then_CarryExtendAndZeroAreSet()
    {
        // add.b d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0xD001);
        cpu.D[0] = 0x123456FF;
        cpu.D[1] = 1;

        cpu.Step();

        Assert.Equal(0x12345600u, cpu.D[0]);
        Assert.True(cpu.C);
        Assert.True(cpu.X);
        Assert.True(cpu.Z);
        Assert.False(cpu.V);
    }

    [Fact]
    public void When_SubtractingWithBorrow_Then_CarryAndExtendAreSet()
    {
        // sub.l d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x9081);
        cpu.D[0] = 0;
        cpu.D[1] = 1;

        cpu.Step();

        Assert.Equal(0xFFFFFFFFu, cpu.D[0]);
        Assert.True(cpu.C);
        Assert.True(cpu.X);
        Assert.True(cpu.N);
        Assert.False(cpu.V);
    }

    [Fact]
    public void When_ComparingEqualValues_Then_ZeroIsSetAndExtendIsUnchanged()
    {
        // cmp.w d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0xB041);
        cpu.D[0] = 5;
        cpu.D[1] = 5;
        cpu.X = true;

        cpu.Step();

        Assert.True(cpu.Z);
        Assert.True(cpu.X);
        Assert.False(cpu.C);
        Assert.Equal(5u, cpu.D[0]);
    }

    [Fact]
    public void When_AddingWithExtendResultingInZero_Then_ZeroFlagIsUnchanged()
    {
        // addx.l d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0xD181);
        cpu.D[0] = 0xFFFFFFFF;
        cpu.D[1] = 0;
        cpu.X = true;
        cpu.Z = true;

        cpu.Step();

        Assert.Equal(0u, cpu.D[0]);
        Assert.True(cpu.Z);
        Assert.True(cpu.C);
        Assert.True(cpu.X);
    }

    [Fact]
    public void When_Negating_Then_ResultAndFlagsAreSet()
    {
        // neg.l d0
        var cpu = CreateCpu(CpuType.M68000, 0x4480);
        cpu.D[0] = 1;

        cpu.Step();

        Assert.Equal(0xFFFFFFFFu, cpu.D[0]);
        Assert.True(cpu.C);
        Assert.True(cpu.X);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_MultiplyingWords_Then_LongResultIsStored()
    {
        // mulu.w d1,d0 ; muls.w d3,d2
        var cpu = CreateCpu(CpuType.M68000, 0xC0C1, 0xC5C3);
        cpu.D[0] = 0xFFFF;
        cpu.D[1] = 0xFFFF;
        cpu.D[2] = 0xFFFF;
        cpu.D[3] = 2;

        Step(cpu, 2);

        Assert.Equal(0xFFFE0001u, cpu.D[0]);
        Assert.Equal(0xFFFFFFFEu, cpu.D[2]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_DividingUnsigned_Then_QuotientAndRemainderAreStored()
    {
        // divu.w d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x80C1);
        cpu.D[0] = 100;
        cpu.D[1] = 7;

        cpu.Step();

        Assert.Equal(0x0002000Eu, cpu.D[0]);
        Assert.False(cpu.V);
    }

    [Fact]
    public void When_DividingSigned_Then_RemainderHasSignOfDividend()
    {
        // divs.w d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x81C1);
        cpu.D[0] = unchecked((uint)-100);
        cpu.D[1] = 7;

        cpu.Step();

        Assert.Equal(0xFFFEFFF2u, cpu.D[0]);
        Assert.True(cpu.N);
    }

    [Fact]
    public void When_DividingWithOverflow_Then_OverflowIsSetAndRegisterIsUnchanged()
    {
        // divu.w d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x80C1);
        cpu.D[0] = 0x10000;
        cpu.D[1] = 1;

        cpu.Step();

        Assert.Equal(0x10000u, cpu.D[0]);
        Assert.True(cpu.V);
    }

    [Fact]
    public void When_DividingByZeroWithoutHandler_Then_CpuExceptionIsThrown()
    {
        // divu.w d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x80C1);
        cpu.D[0] = 1;
        cpu.D[1] = 0;

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.ZeroDivide, exception.Vector);
    }

    [Theory]
    // asl.l #1,d0
    [InlineData(0xE380, 0x40000000u, 0u, false, 0x80000000u, false, true, false)]
    // lsr.w #1,d0
    [InlineData(0xE248, 0x00000001u, 0u, false, 0x00000000u, true, false, true)]
    // rol.b #1,d0
    [InlineData(0xE318, 0x00000081u, 0u, false, 0x00000003u, true, false, false)]
    // roxr.b #1,d0 with extend set
    [InlineData(0xE210, 0x00000000u, 0u, true, 0x00000080u, false, false, false)]
    // asr.l d1,d0 with count 4
    [InlineData(0xE2A0, 0x80000000u, 4u, false, 0xF8000000u, false, false, false)]
    // lsl.l d1,d0 with count 0
    [InlineData(0xE3A8, 0x00000001u, 0u, true, 0x00000001u, false, false, true)]
    public void When_Shifting_Then_ResultAndFlagsAreSet(ushort opcode, uint value, uint count, bool extend,
        uint expectedValue, bool expectedCarry, bool expectedOverflow, bool expectedExtend)
    {
        var cpu = CreateCpu(CpuType.M68000, opcode);
        cpu.D[0] = value;
        cpu.D[1] = count;
        cpu.X = extend;

        cpu.Step();

        Assert.Equal(expectedValue, cpu.D[0]);
        Assert.Equal(expectedCarry, cpu.C);
        Assert.Equal(expectedOverflow, cpu.V);
        Assert.Equal(expectedExtend, cpu.X);
    }

    [Fact]
    public void When_TestingAndSettingBits_Then_ZeroFlagReflectsPreviousBit()
    {
        // btst #3,d0 ; bset d1,d2 ; bclr #0,(a0)
        var cpu = CreateCpu(CpuType.M68000, 0x0800, 0x0003, 0x03C2, 0x0890, 0x0000);
        cpu.D[0] = 8;
        cpu.D[1] = 33;
        cpu.D[2] = 0;
        cpu.A[0] = 0x3000;
        cpu.Ram.WriteByte(0x3000, 0x01);

        cpu.Step();
        Assert.False(cpu.Z);

        cpu.Step();
        Assert.True(cpu.Z);
        Assert.Equal(2u, cpu.D[2]);

        cpu.Step();
        Assert.False(cpu.Z);
        Assert.Equal(0, cpu.Ram.ReadByte(0x3000));
    }

    [Fact]
    public void When_AddingAndSubtractingBcd_Then_DecimalResultsAreCalculated()
    {
        // abcd d1,d0 ; sbcd d3,d2 ; nbcd d4
        var cpu = CreateCpu(CpuType.M68000, 0xC101, 0x8503, 0x4804);
        cpu.D[0] = 0x45;
        cpu.D[1] = 0x38;
        cpu.D[2] = 0x45;
        cpu.D[3] = 0x38;
        cpu.D[4] = 0x01;

        cpu.Step();
        Assert.Equal(0x83u, cpu.D[0]);
        Assert.False(cpu.C);

        cpu.Step();
        Assert.Equal(0x07u, cpu.D[2]);
        Assert.False(cpu.C);

        cpu.Step();
        Assert.Equal(0x99u, cpu.D[4]);
        Assert.True(cpu.C);
    }

    [Fact]
    public void When_AddingBcdWithCarry_Then_CarryIsSetAndZeroIsUnchanged()
    {
        // abcd d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0xC101);
        cpu.D[0] = 0x99;
        cpu.D[1] = 0x01;
        cpu.Z = true;

        cpu.Step();

        Assert.Equal(0x00u, cpu.D[0]);
        Assert.True(cpu.C);
        Assert.True(cpu.X);
        Assert.True(cpu.Z);
    }

    [Fact]
    public void When_LoopingWithDbf_Then_LoopIsExecutedCountPlusOneTimes()
    {
        var code = new M68kAssembler()
            .Moveq(4, 0)
            .Moveq(0, 1)
            .Label("loop")
            .Addq(M68kAssembler.Size.Long, 1, M68kAssembler.D(1))
            .Dbf(0, "loop")
            .Nop()
            .ToArray();
        var cpu = CreateCpu(CpuType.M68000, code);

        Step(cpu, 12);

        Assert.Equal(5u, cpu.D[1]);
        Assert.Equal(0xFFFFu, cpu.D[0] & 0xFFFF);
        Assert.Equal(0x4E71, cpu.Ram.ReadWord(cpu.PC));
    }

    [Fact]
    public void When_CallingSubroutine_Then_ReturnAddressIsPushedAndPopped()
    {
        var code = new M68kAssembler()
            .Bsr("sub")
            .Moveq(2, 1)
            .Nop()
            .Label("sub")
            .Moveq(1, 0)
            .Rts()
            .ToArray();
        var cpu = CreateCpu(CpuType.M68000, code);

        cpu.Step();
        Assert.Equal(SupervisorStack - 4, cpu.A[7]);
        Assert.Equal(CodeAddress + 4, cpu.Ram.ReadLong(cpu.A[7]));

        Step(cpu, 3);
        Assert.Equal(1u, cpu.D[0]);
        Assert.Equal(2u, cpu.D[1]);
        Assert.Equal(SupervisorStack, cpu.A[7]);
    }

    [Fact]
    public void When_LinkingAndUnlinking_Then_FramePointerAndStackAreRestored()
    {
        // link a6,#-8 ; unlk a6
        var cpu = CreateCpu(CpuType.M68000, 0x4E56, 0xFFF8, 0x4E5E);
        cpu.A[6] = 0x12345678;

        cpu.Step();
        Assert.Equal(SupervisorStack - 4, cpu.A[6]);
        Assert.Equal(SupervisorStack - 12, cpu.A[7]);
        Assert.Equal(0x12345678u, cpu.Ram.ReadLong(SupervisorStack - 4));

        cpu.Step();
        Assert.Equal(0x12345678u, cpu.A[6]);
        Assert.Equal(SupervisorStack, cpu.A[7]);
    }

    [Fact]
    public void When_MovingMultipleRegisters_Then_RegistersAreSavedAndRestored()
    {
        // movem.l d0-d1/a0,-(a7) ; movem.l (a7)+,d2-d3/a1
        var cpu = CreateCpu(CpuType.M68000, 0x48E7, 0xC080, 0x4CDF, 0x020C);
        cpu.D[0] = 0x11111111;
        cpu.D[1] = 0x22222222;
        cpu.A[0] = 0x33333333;

        cpu.Step();
        Assert.Equal(SupervisorStack - 12, cpu.A[7]);
        Assert.Equal(0x11111111u, cpu.Ram.ReadLong(SupervisorStack - 12));
        Assert.Equal(0x33333333u, cpu.Ram.ReadLong(SupervisorStack - 4));

        cpu.Step();
        Assert.Equal(0x11111111u, cpu.D[2]);
        Assert.Equal(0x22222222u, cpu.D[3]);
        Assert.Equal(0x33333333u, cpu.A[1]);
        Assert.Equal(SupervisorStack, cpu.A[7]);
    }

    [Fact]
    public void When_ExtendingAndSwapping_Then_ValuesAreSignExtendedAndSwapped()
    {
        // ext.w d0 ; ext.l d1 ; swap d2
        var cpu = CreateCpu(CpuType.M68000, 0x4880, 0x48C1, 0x4842);
        cpu.D[0] = 0x12345680;
        cpu.D[1] = 0x00008000;
        cpu.D[2] = 0x12345678;

        Step(cpu, 3);

        Assert.Equal(0x1234FF80u, cpu.D[0]);
        Assert.Equal(0xFFFF8000u, cpu.D[1]);
        Assert.Equal(0x56781234u, cpu.D[2]);
    }

    [Fact]
    public void When_UsingIndexedAndPcRelativeAddressing_Then_EffectiveAddressesAreCalculated()
    {
        // move.l 4(a0,d1.w),d0 ; move.w 4(pc),d2 ; nop ; dc.w $beef
        var cpu = CreateCpu(CpuType.M68000, 0x2030, 0x1004, 0x343A, 0x0004, 0x4E71, 0xBEEF);
        cpu.A[0] = 0x3000;
        cpu.D[1] = 0xFFFF0008;
        cpu.Ram.WriteLong(0x300C, 0xCAFEBABE);

        Step(cpu, 2);

        Assert.Equal(0xCAFEBABEu, cpu.D[0]);
        Assert.Equal(0xBEEFu, cpu.D[2] & 0xFFFF);
    }

    [Fact]
    public void When_ReadingWordFromOddAddressOn68000_Then_AddressErrorIsRaised()
    {
        // move.w (a0),d0
        var cpu = CreateCpu(CpuType.M68000, 0x3010);
        cpu.A[0] = 0x3001;

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.AddressError, exception.Vector);
    }

    [Fact]
    public void When_ReadingWordFromOddAddressOn68020_Then_WordIsRead()
    {
        // move.w (a0),d0
        var cpu = CreateCpu(CpuType.M68020, 0x3010);
        cpu.A[0] = 0x3001;
        cpu.Ram.WriteByte(0x3001, 0x12);
        cpu.Ram.WriteByte(0x3002, 0x34);

        cpu.Step();

        Assert.Equal(0x1234u, cpu.D[0]);
    }

    [Fact]
    public void When_ExecutingIllegalInstruction_Then_IllegalInstructionIsRaised()
    {
        var cpu = CreateCpu(CpuType.M68000, 0x4AFC);

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.IllegalInstruction, exception.Vector);
        Assert.Equal(CodeAddress, exception.Pc);
    }

    [Fact]
    public void When_ExecutingPrivilegedInstructionInUserMode_Then_PrivilegeViolationIsRaised()
    {
        // move #$2700,sr
        var cpu = CreateCpu(CpuType.M68000, 0x46FC, 0x2700);
        EnterUserMode(cpu);

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.PrivilegeViolation, exception.Vector);
    }

    [Theory]
    [InlineData(CpuType.M68000, 6u)]
    [InlineData(CpuType.M68020, 8u)]
    public void When_TrapWithHandlerReturnsWithRte_Then_ExecutionContinuesInUserMode(CpuType type,
        uint frameSize)
    {
        // trap #0 ; nop
        var cpu = CreateCpu(type, 0x4E40, 0x4E71);
        cpu.Ram.WriteLong(ExceptionVectors.Trap0 * 4, 0x2000);
        cpu.Ram.WriteWord(0x2000, 0x4E73); // rte
        EnterUserMode(cpu);

        cpu.Step();
        Assert.True(cpu.Supervisor);
        Assert.Equal(0x2000u, cpu.PC);
        Assert.Equal(SupervisorStack - frameSize, cpu.A[7]);
        Assert.Equal(CodeAddress + 2, cpu.Ram.ReadLong(cpu.A[7] + 2));

        cpu.Step();
        Assert.False(cpu.Supervisor);
        Assert.Equal(CodeAddress + 2, cpu.PC);
        Assert.Equal(UserStack, cpu.A[7]);
    }

    [Fact]
    public void When_SettingConditionally_Then_ByteIsSetToAllOnesOrZero()
    {
        // seq d0 ; sne d1
        var cpu = CreateCpu(CpuType.M68000, 0x57C0, 0x56C1);
        cpu.D[0] = 0x12345600;
        cpu.D[1] = 0x123456FF;
        cpu.Z = true;

        Step(cpu, 2);

        Assert.Equal(0x123456FFu, cpu.D[0]);
        Assert.Equal(0x12345600u, cpu.D[1]);
    }

    [Fact]
    public void When_ExecutingLineAOpcodeWithHandler_Then_HandlerIsCalled()
    {
        var cpu = CreateCpu(CpuType.M68000, 0xA005, 0x4E71);
        ushort handledOpcode = 0;
        cpu.LineAHandler = (_, opcode) =>
        {
            handledOpcode = opcode;
            return true;
        };

        cpu.Step();

        Assert.Equal(0xA005, handledOpcode);
        Assert.Equal(CodeAddress + 2, cpu.PC);
    }

    [Fact]
    public void When_Executing68020InstructionOn68000_Then_IllegalInstructionIsRaised()
    {
        // mulu.l d1,d0
        var cpu = CreateCpu(CpuType.M68000, 0x4C01, 0x0000);

        var exception = Assert.Throws<CpuException>(() => cpu.Step());

        Assert.Equal(ExceptionVectors.IllegalInstruction, exception.Vector);
    }
}
