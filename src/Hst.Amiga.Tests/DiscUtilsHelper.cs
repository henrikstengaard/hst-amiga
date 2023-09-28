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

    public static Stream OpenVhd(string path)
    {
        var vhdDisk = VirtualDisk.OpenDisk(path, FileAccess.Read);
        vhdDisk.Content.Position = 0;
        return vhdDisk.Content;        
    }
}