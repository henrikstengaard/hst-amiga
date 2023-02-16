namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Core.Extensions;
using DataTypes.DiskObjects;
using Microsoft.Extensions.Logging;

public class IconUpdateCommand : CommandBase
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

    public IconUpdateCommand(ILogger<IconUpdateCommand> logger, string path, int? type, int? x, int? y, int? stackSize, int? drawerX,
        int? drawerY, int? drawerWidth, int? drawerHeight)
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
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (type.HasValue && (type.Value < 1 || type.Value > 8))
        {
            return new Result(new Error($"Invalid type {type.Value}"));
        }
        
        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);

        if (diskObject.Type is not (Constants.DiskObjectTypes.DISK or Constants.DiskObjectTypes.DRAWER or Constants.DiskObjectTypes.GARBAGE) &&
            drawerX.HasValue)
        {
            return new Result(new Error($"Drawer x not valid for type {diskObject.Type}"));
        }

        if (diskObject.Type is not (Constants.DiskObjectTypes.DISK or Constants.DiskObjectTypes.DRAWER or Constants.DiskObjectTypes.GARBAGE) &&
            drawerY.HasValue)
        {
            return new Result(new Error($"Drawer y not valid for type {diskObject.Type}"));
        }

        if (diskObject.Type is not (Constants.DiskObjectTypes.DISK or Constants.DiskObjectTypes.DRAWER or Constants.DiskObjectTypes.GARBAGE) &&
            drawerWidth.HasValue)
        {
            return new Result(new Error($"Drawer width not valid for type {diskObject.Type}"));
        }

        if (diskObject.Type is not (Constants.DiskObjectTypes.DISK or Constants.DiskObjectTypes.DRAWER or Constants.DiskObjectTypes.GARBAGE) &&
            drawerHeight.HasValue)
        {
            return new Result(new Error($"Drawer height not valid for type {diskObject.Type}"));
        }
        
        //var colorIconBytes = await iconStream.ReadBytes((int)(iconStream.Length - iconStream.Position));

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
        
        if ((diskObject.Type == Constants.DiskObjectTypes.DISK || 
             diskObject.Type == Constants.DiskObjectTypes.DRAWER || 
             diskObject.Type == Constants.DiskObjectTypes.GARBAGE) && 
            diskObject.DrawerData != null)
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
        }
        
        if (!isUpdated)
        {
            return new Result();
        }
        
        iconStream.Position = 0;
        await DiskObjectWriter.Write(diskObject, iconStream);

        return new Result();
    }
}