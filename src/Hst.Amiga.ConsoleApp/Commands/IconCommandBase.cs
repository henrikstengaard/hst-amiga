namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core;
using DataTypes.DiskObjects;
using DataTypes.DiskObjects.ColorIcons;
using DataTypes.DiskObjects.NewIcons;
using Imaging;
using Models;

public abstract class IconCommandBase : CommandBase
{
    protected static void CreateDummyPlanarImages(DiskObject diskObject)
    {
        var image = new Image(1, 1, 8);
        image.Palette.AddColor(0, 0, 0);
        var imageData = ImageDataEncoder.Encode(image, 2);
        diskObject.Gadget.Width = 1;
        diskObject.Gadget.Height = 1;
        diskObject.FirstImageData = imageData;
        diskObject.Gadget.GadgetRenderPointer = 1;
        diskObject.Gadget.SelectRenderPointer = 0;
    }

    protected async Task<Result> ImportIconImages(DiskObject diskObject, ColorIcon colorIcon, ImageType type, string image1Path, string image2Path)
    {
        switch (type)
        {
            case ImageType.Planar:
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Importing planar icon image 1 from file '{image1Path}'");
                    diskObject.FirstImageData = await ImportPlanarImage(image1Path);
                    diskObject.Gadget.GadgetRenderPointer = 1;
                    diskObject.Gadget.LeftEdge = 0;
                    diskObject.Gadget.TopEdge = 0;
                    diskObject.Gadget.Width = diskObject.FirstImageData.Width;
                    diskObject.Gadget.Height = diskObject.FirstImageData.Height;

                    // delete planar icon image 2, if not width or height is equal
                    if (diskObject.SecondImageData != null && 
                        (diskObject.SecondImageData.Width != diskObject.Gadget.Width ||
                        diskObject.SecondImageData.Height != diskObject.Gadget.Height))
                    {
                        diskObject.SecondImageData = null;
                        diskObject.Gadget.SelectRenderPointer = 0;
                    }
                }

                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Importing planar icon image 2 from file '{image2Path}'");

                    if (diskObject.FirstImageData == null)
                    {
                        return new Result(new Error("Import planar icon image 1 first"));
                    }
                    
                    var imageData = await ImportPlanarImage(image2Path);

                    if (imageData.Width != diskObject.Gadget.Width)
                    {
                        return new Result(new Error($"Planar icon image 2 has width {imageData.Width} and is not equal to icon image planar width {diskObject.Gadget.Width}"));
                    }

                    if (imageData.Height != diskObject.Gadget.Height)
                    {
                        return new Result(new Error($"Planar icon image 2 has height {imageData.Height} and is not equal to icon image planar height {diskObject.Gadget.Height}"));
                    }
                    
                    diskObject.SecondImageData = imageData;
                    diskObject.Gadget.SelectRenderPointer = 1;
                }
                break;
            case ImageType.NewIcon:
                if (diskObject.FirstImageData == null)
                {
                    CreateDummyPlanarImages(diskObject);
                }
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Importing new icon image 1 from file '{image1Path}'");
                    NewIconHelper.SetNewIconImage(diskObject, 1, await ImportNewIconImage(image1Path));
                }
                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Importing new icon image 2 from file '{image2Path}'");
                    NewIconHelper.SetNewIconImage(diskObject, 2, await ImportNewIconImage(image2Path));
                }
                break;
            case ImageType.ColorIcon:
                if (diskObject.FirstImageData == null)
                {
                    CreateDummyPlanarImages(diskObject);
                }
                if (!string.IsNullOrWhiteSpace(image1Path))
                {
                    OnInformationMessage($"Importing color icon image 1 from file '{image1Path}'");
                    var colorIconImage = await ImportColorIconImage(image1Path);

                    if (colorIcon.Images.Length == 0)
                    {
                        colorIcon.Images = new[] { colorIconImage };
                    }
                    else
                    {
                        colorIcon.Images[0] = colorIconImage;
                    }
                    colorIcon.Width = colorIconImage.Image.Width;
                    colorIcon.Height = colorIconImage.Image.Height;
                }
                if (!string.IsNullOrWhiteSpace(image2Path))
                {
                    OnInformationMessage($"Importing color icon image 2 from file '{image2Path}'");
                    var colorIconImage = await ImportColorIconImage(image2Path);

                    switch (colorIcon.Images.Length)
                    {
                        case 0:
                            throw new ArgumentException("Icon doesn't have 1st color icon image", nameof(image2Path));
                        case 1:
                            colorIcon.Images = colorIcon.Images.Concat(new[] { colorIconImage }).ToArray();
                            break;
                        default:
                            colorIcon.Images[1] = colorIconImage;
                            break;
                    }
                }
                break;
        }

        return new Result();
    }
    
    protected static async Task<ImageData> ImportPlanarImage(string imagePath)
    {
        var image = await ReadImage(imagePath);
        if (image.BitsPerPixel != 8)
        {
            image = ImageConverter.To8Bpp(image);
        }
        return ImageDataEncoder.Encode(image);
    }

    protected static async Task<NewIcon> ImportNewIconImage(string imagePath)
    {
        var image = await ReadImage(imagePath);
        if (image.BitsPerPixel != 8)
        {
            image = ImageConverter.To8Bpp(image);
        }
        return NewIconConverter.ToNewIcon(image);
    }

    protected static async Task<ColorIconImage> ImportColorIconImage(string imagePath)
    {
        var image = await ReadImage(imagePath);
        if (image.BitsPerPixel != 8)
        {
            image = ImageConverter.To8Bpp(image);
        }
        return new ColorIconImage
        {
            Image = image,
            Depth = DiskObjectHelper.CalculateDepth(image.Palette.Colors.Count)
        };
    }
    
    protected static Task<Image> ReadImage(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            throw new ArgumentException($"Image '{imagePath}' doesn't exist", nameof(imagePath));
        }
        var extension = Path.GetExtension(imagePath);
        switch (extension)
        {
            case ".bmp":
                return Task.FromResult(Imaging.Bitmaps.BitmapReader.Read(File.OpenRead(imagePath)));
            case ".png":
                return Task.FromResult(Imaging.Pngcs.PngReader.Read(File.OpenRead(imagePath)));
            default:
                throw new IOException($"Image format '{extension}' is not supported");
        }
    }

    protected static async Task WriteIcon(Stream stream, DiskObject diskObject)
    {
        stream.Position = 0;
        await DiskObjectWriter.Write(diskObject, stream);
    }

    protected static async Task WriteColorIcon(Stream stream, ColorIcon colorIcon)
    {
        if (colorIcon.Images.Length == 0)
        {
            return;
        }
        
        stream.SetLength(stream.Position);
        await ColorIconWriter.Write(stream, colorIcon, true, true);
    }
    
    protected static void DeleteAllIconImages(DiskObject diskObject, ColorIcon colorIcon)
    {
        CreateDummyPlanarImages(diskObject);
        NewIconHelper.RemoveNewIconImages(diskObject);
        colorIcon.Images = Array.Empty<ColorIconImage>();
    }

    protected static void SetDrawerFlags(DiskObject diskObject, DrawerFlags drawerFlags)
    {
        switch (drawerFlags)
        {
            case DrawerFlags.IconsOnly:
                DiskObjectHelper.SetDrawerData2Flags(diskObject, DrawerData2.FlagEnum.ViewIcons);
                break;
            case DrawerFlags.AllFiles:
                DiskObjectHelper.SetDrawerData2Flags(diskObject, DrawerData2.FlagEnum.ViewIcons | DrawerData2.FlagEnum.AllFiles);
                break;
            case DrawerFlags.Os1X:
            default:
                DiskObjectHelper.SetDrawerData2Flags(diskObject, DrawerData2.FlagEnum.ViewIconsOs1X);
                break;
        }
    }

    protected static void SetDrawerViewModes(DiskObject diskObject, DrawerViewModes drawerViewMode)
    {
        switch (drawerViewMode)
        {
            case DrawerViewModes.Icons:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowIcons);
                break;
            case DrawerViewModes.SortedByDate:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowSortedByDate);
                break;
            case DrawerViewModes.SortedByName:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowSortedByName);
                break;
            case DrawerViewModes.SortedBySize:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowSortedBySize);
                break;
            case DrawerViewModes.SortedByType:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowSortedByType);
                break;
            case DrawerViewModes.Os1X:
            default:
                DiskObjectHelper.SetDrawerData2ViewMode(diskObject, DrawerData2.ViewModesEnum.ShowIconsOs1X);
                break;
        }
    }
}