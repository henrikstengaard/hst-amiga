namespace Hst.Amiga.Tests.VmTests;

using Vm;
using Vm.Libraries;
using Vm.Libraries.Exec;
using Vm.Libraries.Structs;
using Xunit;

public class GivenResidentModules
{
    private readonly VmContext context;
    private readonly ExecLibrary exec;

    public GivenResidentModules()
    {
        context = new VmContext(new VmOptions { Log = _ => { } });
        exec = new ExecLibrary(context);
        exec.Install();
        context.AddLibrary(exec);
        var version = new VersionLibrary(context, exec);
        version.Install();
        context.AddLibrary(version);
        var stub = new StubLibrary(context, exec, "intuition.library");
        stub.Install();
        context.AddLibrary(stub);
        exec.SetResidentModules(context.Libraries);

        var stack = context.Allocator.Allocate(4096);
        context.Cpu.A[7] = stack + 4096;
    }

    private uint FindResident(string name)
    {
        var nameAddress = context.Allocator.Allocate((uint)name.Length + 1);
        context.Ram.WriteCString(nameAddress, name);
        context.Cpu.A[1] = nameAddress;
        return context.CallSubroutine((uint)(exec.BaseAddress + exec.GetOffset("FindResident")));
    }

    [Fact]
    public void When_OpeningVersionLibrary_Then_WorkbenchVersionIsReturned()
    {
        var library = exec.OpenLibrary("version.library", 0);

        Assert.NotEqual(0U, library);
        Assert.Equal(40, context.Ram.ReadWord(library + LibraryStruct.Version));
        Assert.Equal(42, context.Ram.ReadWord(library + LibraryStruct.Revision));
        Assert.Equal(ExecLibrary.KickstartRevision, context.Ram.ReadWord(exec.BaseAddress + ExecBaseStruct.SoftVer));
    }

    [Fact]
    public void When_FindingResidentModules_Then_LibrariesExceptStubsAreFound()
    {
        var resident = FindResident("exec.library");

        Assert.NotEqual(0U, resident);
        Assert.Equal(0x4AFC, context.Ram.ReadWord(resident));
        Assert.Equal(40, context.Ram.ReadByte(resident + 11));
        Assert.Equal("exec.library", context.Ram.ReadCString(context.Ram.ReadLong(resident + 14)));
        Assert.NotEqual(0U, FindResident("version.library"));
        Assert.Equal(0U, FindResident("intuition.library"));
        Assert.Equal(0U, FindResident("nosuch.library"));
    }
}
