namespace Hst.Amiga.Vm.Libraries;

using System;
using Exec;

/// <summary>
/// Emulated version.library, which has no functions and only tells the Workbench version with its library
/// version and revision, e.g. used by Version command. Workbench 3.1 is 40.42.
/// </summary>
public class VersionLibrary : Library
{
    private readonly ExecLibrary exec;

    public VersionLibrary(VmContext context, ExecLibrary exec) : base(context)
    {
        this.exec = exec;
    }

    public override string Name => "version.library";
    public override ushort Version => 40;
    public override ushort Revision => 42;
    public override string IdString => "version 40.42 (2.8.94)";
    protected override string[] FunctionNames => Array.Empty<string>();

    protected override void RegisterFunctions()
    {
    }

    protected override void Initialize()
    {
        exec.AddLibraryNode(this);
    }
}
