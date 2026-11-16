namespace Hst.Amiga.Vm.Traps;

using System;
using System.Collections.Generic;
using Processor;

/// <summary>
/// Table of traps calling emulated functions implemented in C#. Traps are line-A opcodes $A000-$AFFF with
/// trap index in lower 12 bits.
/// </summary>
public class TrapTable
{
    public const ushort TrapOpcodeBase = 0xA000;
    public const int MaxTraps = 0x1000;

    private readonly List<Trap> traps = new();

    private class Trap
    {
        public string Name;
        public Action<M68kCpu> Handler;
    }

    public int Count => traps.Count;

    /// <summary>
    /// Register trap handler.
    /// </summary>
    /// <returns>Trap opcode calling handler.</returns>
    public ushort Register(string name, Action<M68kCpu> handler)
    {
        if (traps.Count >= MaxTraps)
        {
            throw new InvalidOperationException("Trap table is full");
        }

        traps.Add(new Trap { Name = name, Handler = handler });
        return (ushort)(TrapOpcodeBase | (traps.Count - 1));
    }

    public string GetName(ushort opcode)
    {
        var index = opcode & 0xFFF;
        return index < traps.Count ? traps[index].Name : null;
    }

    /// <summary>
    /// Handle line-A opcode by calling registered trap handler.
    /// </summary>
    /// <returns>True, if opcode is a registered trap.</returns>
    public bool Handle(M68kCpu cpu, ushort opcode)
    {
        var index = opcode & 0xFFF;
        if (index >= traps.Count)
        {
            return false;
        }

        traps[index].Handler(cpu);
        return true;
    }
}
