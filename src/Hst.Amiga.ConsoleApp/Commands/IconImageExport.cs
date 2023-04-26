namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using DataTypes.DiskObjects.NewIcons;
using Imaging;
using Microsoft.Extensions.Logging;

public class IconImageExport : CommandBase
{
    private readonly ILogger<IconImageExport> logger;
    private readonly string path;
    private readonly ImageType type;
    private readonly string image1Path;
    private readonly string image2Path;
    private readonly string palettePath;

    public IconImageExport(ILogger<IconImageExport> logger, string path, ImageType type, string image1Path,
        string image2Path, string palettePath)
    {
        this.logger = logger;
        this.path = path;
        this.type = type;
        this.image1Path = image1Path;
        this.image2Path = image2Path;
        this.palettePath = palettePath;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(image1Path) && string.IsNullOrWhiteSpace(image2Path))
        {
            return new Result(new Error("Image 1 and Image 2 not defined, no icon image to export"));
        }

        await using var iconStream = File.OpenRead(path);
        var diskObject = await DiskObjectReader.Read(iconStream);
        var colorIcon = iconStream.Position < iconStream.Length
            ? await ColorIconReader.Read(iconStream)
            : new ColorIcon();

        switch (type)
        {
            case ImageType.Planar:
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Exporting planar image 1 to file '{image1Path}'");
                    var image = ImageDataDecoder.Decode(diskObject.FirstImageData,
                        GetPalette(diskObject.FirstImageData));
                    WriteImage(image1Path, image);
                }

                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Exporting planar image 2 to file '{image2Path}'");
                    var image = ImageDataDecoder.Decode(diskObject.SecondImageData,
                        GetPalette(diskObject.SecondImageData));
                    WriteImage(image2Path, image);
                }

                break;
            case ImageType.NewIcon:
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Exporting new icon image 1 to file '{image1Path}'");
                    var newIcon = NewIconHelper.GetNewIconImage(diskObject, 1);
                    var image = NewIconConverter.ToImage(newIcon);
                    WriteImage(image1Path, image);
                }

                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Exporting new icon image 2 to file '{image2Path}'");
                    var newIcon = NewIconHelper.GetNewIconImage(diskObject, 2);
                    var image = NewIconConverter.ToImage(newIcon);
                    WriteImage(image2Path, image);
                }

                break;
            case ImageType.ColorIcon:
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Exporting color icon image 1 to file '{image1Path}'");
                    if (colorIcon.Images.Length < 1)
                    {
                        throw new ArgumentException("Icon doesn't have color icon image 1", nameof(image1Path));
                    }

                    WriteImage(image1Path, colorIcon.Images[0].Image);
                }

                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Exporting color icon image 2 to file '{image2Path}'");
                    if (colorIcon.Images.Length < 2)
                    {
                        throw new ArgumentException("Icon doesn't have color icon image 2", nameof(image2Path));
                    }

                    WriteImage(image2Path, colorIcon.Images[1].Image);
                }

                break;
        }

        return new Result();
    }

    private Palette GetPalette(ImageData imageData)
    {
        if (string.IsNullOrWhiteSpace(palettePath))
        {
            if (imageData.Depth <= 2)
            {
                OnInformationMessage($"Using Amiga OS 3.1 4 color palette");
                return AmigaOs31Palette.FourColors();
            }
            OnInformationMessage($"Using Amiga OS 3.1 full color palette");
            return AmigaOs31Palette.FullPalette();
        }

        OnInformationMessage($"Reading palette from JSON file '{palettePath}'");

        if (!File.Exists(palettePath))
        {
            throw new ArgumentException($"JSON Palette path '{palettePath}' doesn't exist", nameof(palettePath));
        }

        var colors = JsonSerializer.Deserialize<IEnumerable<Models.Color>>(File.OpenRead(palettePath),
            new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
        var palette = new Palette(colors.Select(x => new Color(x.R, x.G, x.B, x.A ?? 255)));

        OnInformationMessage(
            $"Palette has '{palette.Colors.Count}' color{(palette.Colors.Count == 1 ? string.Empty : "s")}");

        return palette;
    }

    private static void WriteImage(string path, Image image)
    {
        var extension = Path.GetExtension(path);
        switch (extension)
        {
            case ".bmp":
                Imaging.Bitmaps.BitmapWriter.Write(File.Open(path, FileMode.Create, FileAccess.ReadWrite), image);
                break;
            case ".png":
                Imaging.Pngcs.PngWriter.Write(File.Open(path, FileMode.Create, FileAccess.ReadWrite), image);
                break;
            default:
                throw new IOException($"Image format '{extension}' is not supported");
        }
    }
}