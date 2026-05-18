namespace Hst.Amiga.Tests;

using System.IO;
using DiscUtils;

public static class DiscUtilsHelper
{
    public static void Setup()
    {
        DiscUtils.Containers.SetupHelper.SetupContainers();
        DiscUtils.FileSystems.SetupHelper.SetupFileSystems();
    }

    public static Stream OpenVhd(string path, bool writable = false)
    {
        var vhdDisk = VirtualDisk.OpenDisk(path, writable ? FileAccess.ReadWrite : FileAccess.Read);
        vhdDisk.Content.Position = 0;
        return vhdDisk.Content;        
    }
}