namespace Hst.Amiga.Vm.Libraries;

using System.Linq;
using Exec;

/// <summary>
/// Stub library, which can be opened, but stops program with unimplemented function for any function called.
/// Used for libraries opened by compiler startup code without being used by CLI programs, e.g. intuition.library.
/// </summary>
public class StubLibrary : Library
{
    private const int FunctionCount = 200;

    private readonly ExecLibrary exec;
    private readonly string[] functionNames;

    public StubLibrary(VmContext context, ExecLibrary exec, string name, ushort version = 40) : base(context)
    {
        this.exec = exec;
        Name = name;
        Version = version;
        functionNames = Enumerable.Range(0, FunctionCount)
            .Select(i => $"Function{-(FirstFunctionOffset + i * JumpTableEntrySize)}").ToArray();
    }

    public override string Name { get; }
    public override ushort Version { get; }
    protected override string[] FunctionNames => functionNames;

    protected override void RegisterFunctions()
    {
    }

    protected override void Initialize()
    {
        exec.AddLibraryNode(this);
    }
}
