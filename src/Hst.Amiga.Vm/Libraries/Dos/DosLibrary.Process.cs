namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.Linq;
using Structs;

public partial class DosLibrary
{
    private readonly Dictionary<uint, List<uint>> readArgsAllocations = new();

    private void RegisterProcessFunctions()
    {
        Register("IoErr", () => D0 = (uint)IoErrValue);
        Register("SetIoErr", () =>
        {
            var old = IoErrValue;
            IoErrValue = (int)D1;
            D0 = (uint)old;
        });
        Register("Fault", () =>
        {
            var code = (int)D1;
            var header = D2 == 0 ? null : Ram.ReadCString(D2);
            var length = (int)D4;
            var message = FaultMessage(code, header);
            if (D3 == 0 || length <= 0)
            {
                D0 = 0;
                return;
            }

            WriteString(D3, length, message);
            D0 = (uint)Math.Min(Ram.GetBytes(message).Length, length - 1);
        });
        Register("PrintFault", () =>
        {
            var code = (int)D1;
            var header = D2 == 0 ? null : Ram.ReadCString(D2);
            if (code != 0)
            {
                WriteString(OutputHandle, FaultMessage(code, header) + "\n");
            }

            IoErrValue = code;
            D0 = code == 0 ? 0 : unchecked((uint)DosTrue);
        });
        Register("Exit", () => throw new ProgramExitException((int)D1, true));
        Register("CheckSignal", () =>
        {
            var received = exec.SignalsReceived & D1;
            exec.SignalsReceived &= ~received;
            D0 = received;
        });

        Register("GetArgStr", () => D0 = Ram.ReadLong(Process + ProcessStruct.Arguments));
        Register("SetArgStr", () =>
        {
            var old = Ram.ReadLong(Process + ProcessStruct.Arguments);
            Ram.WriteLong(Process + ProcessStruct.Arguments, D1);
            D0 = old;
        });
        Register("Cli", () => D0 = CliAddress);
        Register("MaxCli", () => D0 = 1);
        Register("FindCliProc", () => D0 = D1 == 1 ? Process : 0);
        Register("GetConsoleTask", () => D0 = Ram.ReadLong(Process + ProcessStruct.ConsoleTask));
        Register("SetConsoleTask", () => D0 = SwapProcessLong(ProcessStruct.ConsoleTask, D1));
        Register("GetFileSysTask", () => D0 = Ram.ReadLong(Process + ProcessStruct.FileSystemTask));
        Register("SetFileSysTask", () => D0 = SwapProcessLong(ProcessStruct.FileSystemTask, D1));
        Register("GetProgramDir", () => D0 = Ram.ReadLong(Process + ProcessStruct.HomeDir));
        Register("SetProgramDir", () => D0 = SwapProcessLong(ProcessStruct.HomeDir, D1));
        Register("GetProgramName", () => D0 = GetCliString(CliStruct.CommandName, D1, (int)D2));
        Register("SetProgramName", () => D0 = SetCliString(CliStruct.CommandName, Ram.ReadCString(D1)));
        Register("GetCurrentDirName", () => D0 = GetCliString(CliStruct.SetName, D1, (int)D2));
        Register("SetCurrentDirName", () => D0 = SetCliString(CliStruct.SetName, Ram.ReadCString(D1)));
        Register("GetPrompt", () => D0 = GetCliString(CliStruct.Prompt, D1, (int)D2));
        Register("SetPrompt", () => D0 = SetCliString(CliStruct.Prompt, Ram.ReadCString(D1)));

        Register("ReadArgs", ReadArgs);
        Register("FreeArgs", () => FreeArgs(D1));
        Register("FindArg", () =>
        {
            var template = Ram.ReadCString(D1);
            var keyword = Ram.ReadCString(D2);
            var items = ReadArgsParser.ParseTemplate(template);
            var index = items.ToList().FindIndex(x => x.Matches(keyword));
            D0 = (uint)index;
        });
        Register("StrToLong", () =>
        {
            var text = Ram.ReadCString(D1) ?? string.Empty;
            var position = 0;
            while (position < text.Length && (text[position] == ' ' || text[position] == '\t'))
            {
                position++;
            }

            var negative = false;
            if (position < text.Length && (text[position] == '-' || text[position] == '+'))
            {
                negative = text[position] == '-';
                position++;
            }

            var digitsStart = position;
            long value = 0;
            while (position < text.Length && char.IsDigit(text[position]))
            {
                value = value * 10 + (text[position] - '0');
                position++;
            }

            if (position == digitsStart)
            {
                D0 = 0xFFFFFFFF;
                return;
            }

            Ram.WriteLong(D2, (uint)(int)(negative ? -value : value));
            D0 = (uint)position;
        });

        // running other programs is not supported
        Register("Execute", () =>
        {
            Context.Log($"Warning: Execute(\"{Ram.ReadCString(D1)}\") is not supported");
            SetIoErr(DosErrors.NotImplemented);
            D0 = 0;
        });
        Register("SystemTagList", () =>
        {
            Context.Log($"Warning: SystemTagList(\"{Ram.ReadCString(D1)}\") is not supported");
            SetIoErr(DosErrors.NotImplemented);
            D0 = 0xFFFFFFFF;
        });
        Register("RunCommand", RunCommand);
    }

    private uint SwapProcessLong(int offset, uint value)
    {
        var old = Ram.ReadLong(Process + (uint)offset);
        Ram.WriteLong(Process + (uint)offset, value);
        return old;
    }

    private uint GetCliString(int offset, uint buffer, int length)
    {
        var cli = CliAddress;
        var value = cli == 0 ? string.Empty : Ram.ReadBString(BAddr(Ram.ReadLong(cli + (uint)offset))) ?? string.Empty;
        if (WriteString(buffer, length, value))
        {
            return unchecked((uint)DosTrue);
        }

        SetIoErr(DosErrors.LineTooLong);
        return 0;
    }

    private uint SetCliString(int offset, string value)
    {
        var cli = CliAddress;
        if (cli == 0)
        {
            return 0;
        }

        // cli strings are allocated with 256 bytes
        var bstr = BAddr(Ram.ReadLong(cli + (uint)offset));
        Ram.WriteBString(bstr, value ?? string.Empty);
        return unchecked((uint)DosTrue);
    }

    private static string FaultMessage(int code, string header)
    {
        var message = DosErrors.GetMessage(code);
        return string.IsNullOrEmpty(header) ? message : $"{header}: {message}";
    }

    private void ReadArgs()
    {
        var template = Ram.ReadCString(D1) ?? string.Empty;
        var array = D2;
        var rdArgs = D3;

        var items = ReadArgsParser.ParseTemplate(template);

        // read arguments from rdargs source, if set, otherwise from program arguments
        string arguments;
        var sourceBuffer = rdArgs == 0 ? 0 : Ram.ReadLong(rdArgs + RdArgsStruct.SourceBuffer);
        if (sourceBuffer != 0)
        {
            var length = (int)Ram.ReadLong(rdArgs + RdArgsStruct.SourceLength);
            var current = (int)Ram.ReadLong(rdArgs + RdArgsStruct.SourceCurChr);
            var bytes = Ram.ReadBytes(sourceBuffer + (uint)current, Math.Max(0, length - current));
            arguments = Ram.GetString(bytes, 0, bytes.Length);
        }
        else
        {
            arguments = Ram.ReadCString(Ram.ReadLong(Process + ProcessStruct.Arguments)) ?? string.Empty;
        }

        // question mark shows template and reads arguments from input
        if (arguments.Trim() == "?")
        {
            WriteString(OutputHandle, $"{template}: ");
            FlushConsole();
            arguments = ReadInputLine() + "\n";
        }

        var result = ReadArgsParser.Parse(items, arguments);
        if (result.Error != 0)
        {
            SetIoErr(result.Error);
            D0 = 0;
            return;
        }

        var allocatedRdArgs = rdArgs == 0;
        if (allocatedRdArgs)
        {
            rdArgs = Context.Allocator.Allocate(RdArgsStruct.Size);
            if (rdArgs == 0)
            {
                SetIoErr(DosErrors.NoFreeStore);
                D0 = 0;
                return;
            }
        }

        FreeArgsAllocations(rdArgs);
        var allocations = new List<uint>();
        readArgsAllocations[rdArgs] = allocations;
        if (allocatedRdArgs)
        {
            // rdargs allocated by ReadArgs() is freed by FreeArgs()
            allocations.Add(rdArgs);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var value = result.Values[i];
            var entry = array + (uint)i * 4;
            var item = items[i];
            if (value == null)
            {
                continue;
            }

            if (item.Toggle)
            {
                Ram.WriteLong(entry, Ram.ReadLong(entry) == 0 ? unchecked((uint)DosTrue) : 0);
                continue;
            }

            switch (value)
            {
                case bool:
                    Ram.WriteLong(entry, unchecked((uint)DosTrue));
                    break;
                case int number:
                    var numberAddress = AllocateArgs(allocations, 4);
                    Ram.WriteLong(numberAddress, (uint)number);
                    Ram.WriteLong(entry, numberAddress);
                    break;
                case List<string> list:
                    var listAddress = AllocateArgs(allocations, (uint)(list.Count + 1) * 4);
                    for (var j = 0; j < list.Count; j++)
                    {
                        Ram.WriteLong(listAddress + (uint)j * 4, AllocateArgsString(allocations, list[j]));
                    }

                    Ram.WriteLong(entry, listAddress);
                    break;
                case string text:
                    Ram.WriteLong(entry, AllocateArgsString(allocations, text));
                    break;
            }
        }

        D0 = rdArgs;
    }

    private uint AllocateArgs(List<uint> allocations, uint size)
    {
        var address = Context.Allocator.Allocate(size);
        if (address == 0)
        {
            throw new VmException("Not enough memory to allocate arguments");
        }

        allocations.Add(address);
        return address;
    }

    private uint AllocateArgsString(List<uint> allocations, string value)
    {
        var bytes = Ram.GetBytes(value);
        var address = AllocateArgs(allocations, (uint)bytes.Length + 1);
        Ram.WriteBytes(address, bytes);
        return address;
    }

    private void FreeArgsAllocations(uint rdArgs)
    {
        if (!readArgsAllocations.TryGetValue(rdArgs, out var allocations))
        {
            return;
        }

        foreach (var allocation in allocations.Where(x => x != rdArgs))
        {
            Context.Allocator.Free(allocation);
        }

        allocations.RemoveAll(x => x != rdArgs);
    }

    private void FreeArgs(uint rdArgs)
    {
        if (rdArgs == 0 || !readArgsAllocations.TryGetValue(rdArgs, out var allocations))
        {
            return;
        }

        var freeRdArgs = allocations.Contains(rdArgs);
        FreeArgsAllocations(rdArgs);
        readArgsAllocations.Remove(rdArgs);
        if (freeRdArgs)
        {
            Context.Allocator.Free(rdArgs);
        }
    }

    private void RunCommand()
    {
        var segList = D1;
        var parameters = D3;
        var parametersLength = D4;
        if (segList == 0)
        {
            D0 = 0xFFFFFFFF;
            return;
        }

        var savedArguments = Ram.ReadLong(Process + ProcessStruct.Arguments);
        Ram.WriteLong(Process + ProcessStruct.Arguments, parameters);
        try
        {
            Cpu.D[0] = parametersLength;
            Cpu.A[0] = parameters;
            D0 = Context.CallSubroutine(BAddr(segList) + 4);
        }
        finally
        {
            Ram.WriteLong(Process + ProcessStruct.Arguments, savedArguments);
        }
    }
}
