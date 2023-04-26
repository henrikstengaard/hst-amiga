namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using Microsoft.Extensions.Logging;

public class IconCreateCommand : IconCommandBase
{
    private readonly ILogger<IconCreateCommand> logger;
    private readonly string path;
    private readonly IconType type;
    private readonly int? x;
    private readonly int? y;
    private readonly int? stackSize;
    private readonly int? drawerX;
    private readonly int? drawerY;
    private readonly int? drawerWidth;
    private readonly int? drawerHeight;
    private readonly ImageType imageType;
    private readonly string image1Path;
    private readonly string image2Path;

    public IconCreateCommand(ILogger<IconCreateCommand> logger, string path, IconType type, int? x, int? y,
        int? stackSize, int? drawerX, int? drawerY, int? drawerWidth, int? drawerHeight, ImageType imageType, 
        string image1Path, string image2Path)
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
        this.imageType = imageType;
        this.image1Path = image1Path;
        this.image2Path = image2Path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        var diskObject = CreateDiskObject();
        var colorIcon = new ColorIcon();

        if (x.HasValue)
        {
            diskObject.CurrentX = x.Value;
        }
        
        if (y.HasValue)
        {
            diskObject.CurrentY = y.Value;
        }

        if (stackSize.HasValue)
        {
            diskObject.StackSize = stackSize.Value;
        }
        
        if ((diskObject.Type == Constants.DiskObjectTypes.DISK || 
             diskObject.Type == Constants.DiskObjectTypes.DRAWER || 
             diskObject.Type == Constants.DiskObjectTypes.GARBAGE) && 
            diskObject.DrawerData != null)
        {
            if (drawerX.HasValue)
            {
                diskObject.DrawerData.LeftEdge = (short)drawerX.Value;
            }
            
            if (drawerY.HasValue)
            {
                diskObject.DrawerData.TopEdge = (short)drawerY.Value;
            }

            if (drawerWidth.HasValue)
            {
                diskObject.DrawerData.Width = (short)drawerWidth.Value;
            }
            
            if (drawerHeight.HasValue)
            {
                diskObject.DrawerData.Height = (short)drawerHeight.Value;
            }
        }
        
        CreateDummyPlanarImages(diskObject);

        if (!string.IsNullOrWhiteSpace(image1Path) || !string.IsNullOrWhiteSpace(image2Path))
        {
            var result = await ImportIconImages(diskObject, colorIcon, imageType, image1Path, image2Path);
            if (result.IsFaulted)
            {
                return result;
            }
        }
        
        await using var iconStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        await WriteIcon(iconStream, diskObject, colorIcon);

        return new Result();
    }

    private DiskObject CreateDiskObject()
    {
        return type switch
        {
            IconType.Disk => DiskObjectHelper.CreateDiskInfo(),
            IconType.Drawer => DiskObjectHelper.CreateDrawerInfo(),
            IconType.Tool => DiskObjectHelper.CreateToolInfo(),
            IconType.Project => DiskObjectHelper.CreateProjectInfo(),
            IconType.Garbage => DiskObjectHelper.CreateGarbageInfo(),
            _ => throw new ArgumentOutOfRangeException($"Unsupported disk object type '{type}' to create")
        };
    }
}