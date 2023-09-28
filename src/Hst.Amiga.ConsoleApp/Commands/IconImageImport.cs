namespace Hst.Amiga.ConsoleApp.Commands;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using Microsoft.Extensions.Logging;

public class IconImageImport : IconCommandBase
{
    private readonly ILogger<IconImageImport> logger;
    private readonly string path;
    private readonly ImageType imageType;
    private readonly string image1Path;
    private readonly string image2Path;

    public IconImageImport(ILogger<IconImageImport> logger, string path, ImageType imageType, string image1Path,
        string image2Path)
    {
        this.logger = logger;
        this.path = path;
        this.imageType = imageType;
        this.image1Path = image1Path;
        this.image2Path = image2Path;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (imageType == ImageType.Auto)
        {
            return new Result(new Error("Auto image type is not supported for icon image import"));
        }

        if (string.IsNullOrWhiteSpace(image1Path) && string.IsNullOrWhiteSpace(image2Path))
        {
            return new Result(new Error("Image 1 and Image 2 not defined, no icon image to import"));
        }

        await using var iconStream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        var diskObject = await DiskObjectReader.Read(iconStream);
        var colorIcon = iconStream.Position < iconStream.Length
            ? await ColorIconReader.Read(iconStream)
            : new ColorIcon();

        var result = await ImportIconImages(diskObject, colorIcon, imageType, image1Path, image2Path);
        if (result.IsFaulted)
        {
            return result;
        }

        OnInformationMessage($"Writing disk object to icon file '{path}'");

        await WriteIcon(iconStream, diskObject, colorIcon);

        return new Result();
    }
}