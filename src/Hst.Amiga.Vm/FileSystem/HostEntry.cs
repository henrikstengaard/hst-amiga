namespace Hst.Amiga.Vm.FileSystem;

/// <summary>
/// Entry in host directory with Amiga name and name on host file system.
/// </summary>
public class HostEntry
{
    public HostEntry(string amigaName, string hostName)
    {
        AmigaName = amigaName;
        HostName = hostName;
    }

    public string AmigaName { get; }
    public string HostName { get; }
}
