namespace Hst.Amiga.ConsoleApp.Commands;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using Microsoft.Extensions.Logging;
using Models;

public class IconUpdateCommand : IconCommandBase
{
    private readonly ILogger<IconUpdateCommand> logger;
    private readonly string path;
    private readonly int? type;
    private readonly int? x;
    private readonly int? y;
    private readonly int? stackSize;
    private readonly int? drawerX;
    private readonly int? drawerY;
    private readonly int? drawerWidth;
    private readonly int? drawerHeight;
    private readonly DrawerFlags? drawerFlags;
    private readonly DrawerViewModes? drawerViewModes;

    public IconUpdateCommand(ILogger<IconUpdateCommand> logger, string path, int? type, int? x, int? y, int? stackSize,
        int? drawerX,
        int? drawerY, int? drawerWidth, int? drawerHeight, DrawerFlags? drawerFlags, DrawerViewModes? drawerViewModes)
    {
        this.logger = logger;
        this.path = path;
        this.type = type;
        this.x = x;
        this.y = y;
        this.stackSize = stackSize;
        this.drawerX = drawerX;
        this.drawerY = drawerY;
        this.drawerWidth = drawerWidth;
        this.drawerHeight = drawerHeight;
        this.drawerFlags = drawerFlags;
        this.drawerViewModes = drawerViewModes;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (type.HasValue && (type.Value < 1 || type.Value > 8))
        {
            return new Result(new Error($"Invalid type {type.Value}"));
        }

        OnInformationMessage($"Reading disk object from icon file '{path}'");

        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);

        if (!IsDrawerIcon(diskObject) && drawerX.HasValue)
        {
            if (drawerX.HasValue)
            {
                return new Result(new Error($"Drawer x not valid for type {diskObject.Type}"));
            }
            
            if (drawerY.HasValue)
            {
                return new Result(new Error($"Drawer y not valid for type {diskObject.Type}"));
            }

            if (drawerWidth.HasValue)
            {
                return new Result(new Error($"Drawer width not valid for type {diskObject.Type}"));
            }

            if (drawerHeight.HasValue)
            {
                return new Result(new Error($"Drawer height not valid for type {diskObject.Type}"));
            }
            
            if (drawerFlags.HasValue)
            {
                return new Result(new Error($"Drawer flags not valid for type {diskObject.Type}"));
            }
            
            if (drawerViewModes.HasValue)
            {
                return new Result(new Error($"Drawer view mode not valid for type {diskObject.Type}"));
            }
        }
        
        var isUpdated = false;

        if (type.HasValue)
        {
            diskObject.Type = (byte)type.Value;
            isUpdated = true;
        }

        if (x.HasValue)
        {
            diskObject.CurrentX = x.Value;
            isUpdated = true;
        }

        if (y.HasValue)
        {
            diskObject.CurrentY = y.Value;
            isUpdated = true;
        }

        if (stackSize.HasValue)
        {
            diskObject.StackSize = stackSize.Value;
            isUpdated = true;
        }

        if (IsDrawerIcon(diskObject) && diskObject.DrawerData != null)
        {
            if (drawerX.HasValue)
            {
                diskObject.DrawerData.LeftEdge = (short)drawerX.Value;
                isUpdated = true;
            }

            if (drawerY.HasValue)
            {
                diskObject.DrawerData.TopEdge = (short)drawerY.Value;
                isUpdated = true;
            }

            if (drawerWidth.HasValue)
            {
                diskObject.DrawerData.Width = (short)drawerWidth.Value;
                isUpdated = true;
            }

            if (drawerHeight.HasValue)
            {
                diskObject.DrawerData.Height = (short)drawerHeight.Value;
                isUpdated = true;
            }
            
            if (drawerFlags.HasValue)
            {
                SetDrawerFlags(diskObject, drawerFlags.Value);
                isUpdated = true;
            }
            
            if (drawerViewModes.HasValue)
            {
                SetDrawerViewModes(diskObject, drawerViewModes.Value);
                isUpdated = true;
            }
        }

        if (!isUpdated)
        {
            return new Result();
        }

        OnInformationMessage($"Writing disk object to icon file '{path}'");

        await WriteIcon(iconStream, diskObject);

        return new Result();
    }

    private static bool IsDrawerIcon(DiskObject diskObject)
    {
        return diskObject.Type is Constants.DiskObjectTypes.DISK or Constants.DiskObjectTypes.DRAWER
            or Constants.DiskObjectTypes.GARBAGE;
    }
}