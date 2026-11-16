namespace Hst.Amiga.Vm.Processor;

using System;
using System.Collections.Concurrent;

public sealed partial class M68kCpu
{
    /// <summary>
    /// Builds tables of instruction handlers for all 65536 opcodes per cpu type.
    /// </summary>
    private static class OpcodeTableBuilder
    {
        // effective address mode bits
        private const int Dn = 1;
        private const int An = 2;
        private const int Ind = 4;
        private const int PostInc = 8;
        private const int PreDec = 16;
        private const int Disp = 32;
        private const int Index = 64;
        private const int AbsW = 128;
        private const int AbsL = 256;
        private const int PcDisp = 512;
        private const int PcIndex = 1024;
        private const int Imm = 2048;

        private const int All = 0xFFF;
        private const int Data = All & ~An;
        private const int Memory = All & ~(Dn | An);
        private const int Alterable = Dn | An | Ind | PostInc | PreDec | Disp | Index | AbsW | AbsL;
        private const int DataAlterable = Alterable & ~An;
        private const int MemoryAlterable = Alterable & ~(Dn | An);
        private const int Control = Ind | Disp | Index | AbsW | AbsL | PcDisp | PcIndex;
        private const int ControlAlterable = Control & ~(PcDisp | PcIndex);

        private static readonly ConcurrentDictionary<CpuType, Action<M68kCpu>[]> Tables = new();

        public static Action<M68kCpu>[] GetTable(CpuType type) => Tables.GetOrAdd(type, Build);

        private static int EaBit(int ea)
        {
            var mode = (ea >> 3) & 7;
            var reg = ea & 7;
            if (mode < 7)
            {
                return 1 << mode;
            }

            return reg <= 4 ? AbsW << reg : 0;
        }

        private static bool Ea(int opcode, int allowed) => (EaBit(opcode & 0x3F) & allowed) != 0;

        private static bool SizeValid(int opcode) => ((opcode >> 6) & 3) != 3;

        private static bool IsByte(int opcode) => ((opcode >> 6) & 3) == 0;

        private static bool IsAn(int opcode) => ((opcode >> 3) & 7) == 1;

        private static Action<M68kCpu>[] Build(CpuType type)
        {
            var table = new Action<M68kCpu>[0x10000];
            var is020 = type != CpuType.M68000;

            void Add(string pattern, Action<M68kCpu> handler, Func<int, bool> valid = null, bool only020 = false)
            {
                if (only020 && !is020)
                {
                    return;
                }

                pattern = pattern.Replace(" ", string.Empty);
                if (pattern.Length != 16)
                {
                    throw new ArgumentException($"Invalid opcode pattern '{pattern}'");
                }

                var mask = 0;
                var match = 0;
                for (var i = 0; i < 16; i++)
                {
                    var bit = 1 << (15 - i);
                    switch (pattern[i])
                    {
                        case '0':
                            mask |= bit;
                            break;
                        case '1':
                            mask |= bit;
                            match |= bit;
                            break;
                    }
                }

                for (var opcode = 0; opcode < 0x10000; opcode++)
                {
                    if ((opcode & mask) == match && table[opcode] == null && (valid == null || valid(opcode)))
                    {
                        table[opcode] = handler;
                    }
                }
            }

            // line 0: immediate, bit and movep instructions
            Add("0000 0000 0011 1100", cpu => cpu.OpOriCcr());
            Add("0000 0000 0111 1100", cpu => cpu.OpOriSr());
            Add("0000 0010 0011 1100", cpu => cpu.OpAndiCcr());
            Add("0000 0010 0111 1100", cpu => cpu.OpAndiSr());
            Add("0000 1010 0011 1100", cpu => cpu.OpEoriCcr());
            Add("0000 1010 0111 1100", cpu => cpu.OpEoriSr());
            Add("0000 1100 1111 1100", cpu => cpu.OpCas2(), only020: true);
            Add("0000 1110 1111 1100", cpu => cpu.OpCas2(), only020: true);
            Add("0000 xxx1 xx00 1xxx", cpu => cpu.OpMovep());
            Add("0000 xxx1 00xx xxxx", cpu => cpu.OpBitDynamic(), op => Ea(op, Data));
            Add("0000 xxx1 xxxx xxxx", cpu => cpu.OpBitDynamic(), op => Ea(op, DataAlterable));
            Add("0000 1000 00xx xxxx", cpu => cpu.OpBitStatic(), op => Ea(op, Data & ~Imm));
            Add("0000 1000 xxxx xxxx", cpu => cpu.OpBitStatic(), op => Ea(op, DataAlterable));
            Add("0000 0xx0 11xx xxxx", cpu => cpu.OpChk2Cmp2(), op => ((op >> 9) & 3) != 3 && Ea(op, Control),
                true);
            Add("0000 1xx0 11xx xxxx", cpu => cpu.OpCas(), op => ((op >> 9) & 3) != 0 && Ea(op, MemoryAlterable),
                true);
            Add("0000 0000 xxxx xxxx", cpu => cpu.OpOri(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0000 0010 xxxx xxxx", cpu => cpu.OpAndi(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0000 0100 xxxx xxxx", cpu => cpu.OpSubi(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0000 0110 xxxx xxxx", cpu => cpu.OpAddi(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0000 1010 xxxx xxxx", cpu => cpu.OpEori(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0000 1100 xxxx xxxx", cpu => cpu.OpCmpi(),
                op => SizeValid(op) && Ea(op, is020 ? Data & ~Imm : DataAlterable));
            Add("0000 1110 xxxx xxxx", cpu => cpu.OpMoves(), op => SizeValid(op) && Ea(op, MemoryAlterable), true);

            // line 1-3: move
            Add("00xx xxxx xxxx xxxx", cpu => cpu.OpMove(), op =>
            {
                var sizeBits = (op >> 12) & 3;
                if (sizeBits == 0)
                {
                    return false;
                }

                var destination = ((op >> 3) & 0x38) | ((op >> 9) & 7);
                var destinationIsAn = (destination >> 3) == 1;
                if (sizeBits == 1 && (destinationIsAn || IsAn(op)))
                {
                    return false;
                }

                return Ea(op, All) && (destinationIsAn || (EaBit(destination) & DataAlterable) != 0);
            });

            // line 4: miscellaneous
            Add("0100 0000 11xx xxxx", cpu => cpu.OpMoveFromSr(), op => Ea(op, DataAlterable));
            Add("0100 0010 11xx xxxx", cpu => cpu.OpMoveFromCcr(), op => Ea(op, DataAlterable), true);
            Add("0100 0100 11xx xxxx", cpu => cpu.OpMoveToCcr(), op => Ea(op, Data));
            Add("0100 0110 11xx xxxx", cpu => cpu.OpMoveToSr(), op => Ea(op, Data));
            Add("0100 0000 xxxx xxxx", cpu => cpu.OpNegx(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0100 0010 xxxx xxxx", cpu => cpu.OpClr(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0100 0100 xxxx xxxx", cpu => cpu.OpNeg(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0100 0110 xxxx xxxx", cpu => cpu.OpNot(), op => SizeValid(op) && Ea(op, DataAlterable));
            Add("0100 1000 0000 1xxx", cpu => cpu.OpLinkLong(), only020: true);
            Add("0100 1000 00xx xxxx", cpu => cpu.OpNbcd(), op => Ea(op, DataAlterable));
            Add("0100 1000 0100 0xxx", cpu => cpu.OpSwap());
            Add("0100 1000 0100 1xxx", cpu => cpu.IllegalInstruction(), only020: true); // bkpt
            Add("0100 1000 01xx xxxx", cpu => cpu.OpPea(), op => Ea(op, Control));
            Add("0100 1000 1000 0xxx", cpu => cpu.OpExtWord());
            Add("0100 1000 1100 0xxx", cpu => cpu.OpExtLong());
            Add("0100 1001 1100 0xxx", cpu => cpu.OpExtbLong(), only020: true);
            Add("0100 1000 1xxx xxxx", cpu => cpu.OpMovemToMemory(), op => Ea(op, ControlAlterable | PreDec));
            Add("0100 1100 1xxx xxxx", cpu => cpu.OpMovemToRegisters(), op => Ea(op, Control | PostInc));
            Add("0100 1010 1111 1100", cpu => cpu.IllegalInstruction());
            Add("0100 1010 11xx xxxx", cpu => cpu.OpTas(), op => Ea(op, DataAlterable));
            Add("0100 1010 xxxx xxxx", cpu => cpu.OpTst(), op => SizeValid(op) &&
                                                                 (is020
                                                                     ? Ea(op, All) && !(IsByte(op) && IsAn(op))
                                                                     : Ea(op, DataAlterable)));
            Add("0100 1100 00xx xxxx", cpu => cpu.OpMulLong(), op => Ea(op, Data), true);
            Add("0100 1100 01xx xxxx", cpu => cpu.OpDivLong(), op => Ea(op, Data), true);
            Add("0100 1110 0100 xxxx", cpu => cpu.OpTrap());
            Add("0100 1110 0101 0xxx", cpu => cpu.OpLink());
            Add("0100 1110 0101 1xxx", cpu => cpu.OpUnlk());
            Add("0100 1110 0110 xxxx", cpu => cpu.OpMoveUsp());
            Add("0100 1110 0111 0000", cpu => cpu.OpReset());
            Add("0100 1110 0111 0001", _ => { });
            Add("0100 1110 0111 0010", cpu => cpu.OpStop());
            Add("0100 1110 0111 0011", cpu => cpu.OpRte());
            Add("0100 1110 0111 0100", cpu => cpu.OpRtd(), only020: true);
            Add("0100 1110 0111 0101", cpu => cpu.OpRts());
            Add("0100 1110 0111 0110", cpu => cpu.OpTrapv());
            Add("0100 1110 0111 0111", cpu => cpu.OpRtr());
            Add("0100 1110 0111 101x", cpu => cpu.OpMovec(), only020: true);
            Add("0100 1110 10xx xxxx", cpu => cpu.OpJsr(), op => Ea(op, Control));
            Add("0100 1110 11xx xxxx", cpu => cpu.OpJmp(), op => Ea(op, Control));
            Add("0100 xxx1 11xx xxxx", cpu => cpu.OpLea(), op => Ea(op, Control));
            Add("0100 xxx1 10xx xxxx", cpu => cpu.OpChk(2), op => Ea(op, Data));
            Add("0100 xxx1 00xx xxxx", cpu => cpu.OpChk(4), op => Ea(op, Data), true);

            // line 5: addq, subq, scc, dbcc, trapcc
            Add("0101 xxxx 1100 1xxx", cpu => cpu.OpDbcc());
            Add("0101 xxxx 1111 1xxx", cpu => cpu.OpTrapcc(), op => (op & 7) is >= 2 and <= 4, true);
            Add("0101 xxxx 11xx xxxx", cpu => cpu.OpScc(), op => Ea(op, DataAlterable));
            Add("0101 xxx0 xxxx xxxx", cpu => cpu.OpAddq(),
                op => SizeValid(op) && Ea(op, Alterable) && !(IsByte(op) && IsAn(op)));
            Add("0101 xxx1 xxxx xxxx", cpu => cpu.OpSubq(),
                op => SizeValid(op) && Ea(op, Alterable) && !(IsByte(op) && IsAn(op)));

            // line 6: bcc, bra, bsr
            Add("0110 xxxx xxxx xxxx", cpu => cpu.OpBcc());

            // line 7: moveq
            Add("0111 xxx0 xxxx xxxx", cpu => cpu.OpMoveq());

            // line 8: or, div, sbcd, pack, unpk
            Add("1000 xxx1 0000 xxxx", cpu => cpu.OpSbcd());
            Add("1000 xxx1 0100 xxxx", cpu => cpu.OpPack(), only020: true);
            Add("1000 xxx1 1000 xxxx", cpu => cpu.OpUnpk(), only020: true);
            Add("1000 xxx0 11xx xxxx", cpu => cpu.OpDivuWord(), op => Ea(op, Data));
            Add("1000 xxx1 11xx xxxx", cpu => cpu.OpDivsWord(), op => Ea(op, Data));
            Add("1000 xxx0 xxxx xxxx", cpu => cpu.OpOrToRegister(), op => SizeValid(op) && Ea(op, Data));
            Add("1000 xxx1 xxxx xxxx", cpu => cpu.OpOrToEa(), op => SizeValid(op) && Ea(op, MemoryAlterable));

            // line 9: sub, suba, subx
            Add("1001 xxx1 xx00 xxxx", cpu => cpu.OpSubx(), SizeValid);
            Add("1001 xxxx 11xx xxxx", cpu => cpu.OpSuba(), op => Ea(op, All));
            Add("1001 xxx0 xxxx xxxx", cpu => cpu.OpSubToRegister(),
                op => SizeValid(op) && Ea(op, All) && !(IsByte(op) && IsAn(op)));
            Add("1001 xxx1 xxxx xxxx", cpu => cpu.OpSubToEa(), op => SizeValid(op) && Ea(op, MemoryAlterable));

            // line a: traps to emulated functions
            Add("1010 xxxx xxxx xxxx", cpu => cpu.OpLineA());

            // line b: cmp, cmpa, cmpm, eor
            Add("1011 xxxx 11xx xxxx", cpu => cpu.OpCmpa(), op => Ea(op, All));
            Add("1011 xxx1 xx00 1xxx", cpu => cpu.OpCmpm(), SizeValid);
            Add("1011 xxx0 xxxx xxxx", cpu => cpu.OpCmp(),
                op => SizeValid(op) && Ea(op, All) && !(IsByte(op) && IsAn(op)));
            Add("1011 xxx1 xxxx xxxx", cpu => cpu.OpEor(), op => SizeValid(op) && Ea(op, DataAlterable));

            // line c: and, mul, abcd, exg
            Add("1100 xxx1 0000 xxxx", cpu => cpu.OpAbcd());
            Add("1100 xxx1 0100 0xxx", cpu => cpu.OpExg());
            Add("1100 xxx1 0100 1xxx", cpu => cpu.OpExg());
            Add("1100 xxx1 1000 1xxx", cpu => cpu.OpExg());
            Add("1100 xxx0 11xx xxxx", cpu => cpu.OpMuluWord(), op => Ea(op, Data));
            Add("1100 xxx1 11xx xxxx", cpu => cpu.OpMulsWord(), op => Ea(op, Data));
            Add("1100 xxx0 xxxx xxxx", cpu => cpu.OpAndToRegister(), op => SizeValid(op) && Ea(op, Data));
            Add("1100 xxx1 xxxx xxxx", cpu => cpu.OpAndToEa(), op => SizeValid(op) && Ea(op, MemoryAlterable));

            // line d: add, adda, addx
            Add("1101 xxx1 xx00 xxxx", cpu => cpu.OpAddx(), SizeValid);
            Add("1101 xxxx 11xx xxxx", cpu => cpu.OpAdda(), op => Ea(op, All));
            Add("1101 xxx0 xxxx xxxx", cpu => cpu.OpAddToRegister(),
                op => SizeValid(op) && Ea(op, All) && !(IsByte(op) && IsAn(op)));
            Add("1101 xxx1 xxxx xxxx", cpu => cpu.OpAddToEa(), op => SizeValid(op) && Ea(op, MemoryAlterable));

            // line e: shift, rotate, bit field
            Add("1110 1xxx 11xx xxxx", cpu => cpu.OpBitField(), op =>
            {
                var bitFieldType = (op >> 8) & 7;
                var modifies = bitFieldType is 2 or 4 or 6 or 7;
                return Ea(op, Dn | (modifies ? ControlAlterable : Control));
            }, true);
            Add("1110 0xxx 11xx xxxx", cpu => cpu.OpShiftMemory(), op => Ea(op, MemoryAlterable));
            Add("1110 xxxx xxxx xxxx", cpu => cpu.OpShiftRegister(), SizeValid);

            // line f: coprocessor and unimplemented instructions
            Add("1111 xxxx xxxx xxxx", cpu => cpu.OpLineF());

            for (var opcode = 0; opcode < table.Length; opcode++)
            {
                table[opcode] ??= cpu => cpu.IllegalInstruction();
            }

            return table;
        }
    }
}
