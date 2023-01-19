namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using Microsoft.Extensions.Logging;

public class IconInfoCommand : CommandBase
{
    private readonly ILogger<IconInfoCommand> logger;
    private readonly string path;

    public IconInfoCommand(ILogger<IconInfoCommand> logger, string path)
    {
        this.logger = logger;
        this.path = path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        await using var iconStream = File.OpenRead(path);
        var diskObject = await DiskObjectReader.Read(iconStream);

        OnInformationMessage($"Icon type: {diskObject.Type} ({GetIconType(diskObject)})");
        OnInformationMessage($"Icon position x: {diskObject.CurrentX}");
        OnInformationMessage($"Icon position y: {diskObject.CurrentY}");

        if (diskObject.Gadget != null)
        {
            OnInformationMessage($"Window position x: {diskObject.Gadget.LeftEdge}");
            OnInformationMessage($"Window position y: {diskObject.Gadget.TopEdge}");
            OnInformationMessage($"Window width: {diskObject.Gadget.Width}");
            OnInformationMessage($"Window height: {diskObject.Gadget.Height}");
        }

        if (diskObject.FirstImageData != null)
        {
            OnInformationMessage($"First image:");
            OnInformationMessage($"- Width: {diskObject.FirstImageData.Width}");
            OnInformationMessage($"- Height: {diskObject.FirstImageData.Height}");
            OnInformationMessage($"- Depth: {Math.Pow(2, diskObject.FirstImageData.Depth)}");
        }

        if (diskObject.SecondImageData != null)
        {
            OnInformationMessage($"Second image:");
            OnInformationMessage($"- Width: {diskObject.SecondImageData.Width}");
            OnInformationMessage($"- Height: {diskObject.SecondImageData.Height}");
            OnInformationMessage($"- Depth: {Math.Pow(2, diskObject.SecondImageData.Depth)}");
        }
        
        OnInformationMessage("Tool types:");
        OnInformationMessage(string.Join(Environment.NewLine,
            diskObject.ToolTypes.TextDatas.Select(x => AmigaTextHelper.GetString(x.Data))));

        return new Result();
    }

    private static string GetIconType(DiskObject diskObject)
    {
        return diskObject.Type switch
        {
            Constants.DiskObjectTypes.DISK => "Disk",
            Constants.DiskObjectTypes.DRAWER => "Drawer",
            Constants.DiskObjectTypes.TOOL => "Tool",
            Constants.DiskObjectTypes.PROJECT => "Project",
            Constants.DiskObjectTypes.GARBAGE => "Garbage",
            Constants.DiskObjectTypes.DEVICE => "Device",
            Constants.DiskObjectTypes.KICK => "Kick",
            Constants.DiskObjectTypes.APP_ICON => "AppIcon",
            _ => "Unknown"
        };
    }
}