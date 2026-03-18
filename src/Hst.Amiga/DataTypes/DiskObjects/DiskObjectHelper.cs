using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.DiskObjects.ColorIcons;
using Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons;
using Hst.Core.Extensions;

namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amiga;

    public static class DiskObjectHelper
    {
        public static DiskObject CreateInfo()
        {
            return new DiskObject
            {
                CurrentX = Int32.MinValue,
                CurrentY = Int32.MinValue,
                DefaultTool = null,
                DefaultToolPointer = 0,
                DrawerDataPointer = 0,
                DrawerData = null,
                DrawerData2 = null,
                Gadget = new Gadget
                {
                    Activation = (ushort)DefaultGadgetActivationFlags,
                    Flags = (ushort)DefaultGadgetFlags,
                    GadgetId = 0,
                    // GadgetRenderPointer = 1, // indicate first image is present
                    GadgetTextPointer = 0,
                    GadgetType = 1,
                    Height = 10,
                    // Height = (short)firstImage.Height,
                    LeftEdge = 0,
                    MutualExclude = 0,
                    NextPointer = 0,
                    // SelectRenderPointer = (uint)(secondImage != null ? 1 : 0), // indicate second image is present
                    SpecialInfoPointer = 0,
                    TopEdge = 0,
                    UserDataPointer = 1,
                    // Width = (short)firstImage.Width
                    Width = 10
                },
                // SecondImageData = secondImageData,
                StackSize = 4096,
                ToolTypes = null,
                ToolTypesPointer = 0,
                ToolWindowPointer = 0,
                Type = Constants.DiskObjectTypes.PROJECT,
                Pad = 0,
                Version = 1
            };
        }

        public static DrawerData CreateDrawerData(short leftEdge, short topEdge, short width, short height)
        {
            return new DrawerData
            {
                BitMap = 0,
                BlockPen = 255,
                CheckMark = 0,
                CurrentX = 0,
                CurrentY = 0,
                DetailPen = 255,
                FirstGadget = 1,
                Flags = 33559167,
                Height = height, // height of window disk object opens
                IdcmpFlags = 0,
                LeftEdge = leftEdge, // left edge distance of window
                MaxHeight = 65535,
                MaxWidth = 65535,
                MinWidth = 92,
                MinHeight = 65,
                Screen = 0,
                Title = 1,
                TopEdge = topEdge, // top edge distance of window
                Type = 1,
                Width = width // width of window disk object opens
            };
        }

        public static DiskObject CreateDiskInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = (ushort)DefaultGadgetActivationFlags;
            diskObject.Type = Constants.DiskObjectTypes.DISK;
            diskObject.DrawerData = CreateDrawerData(10, 10, 460, 200);
            diskObject.DrawerData.Flags = 33559167;
            diskObject.DrawerDataPointer = 1;
            diskObject.DrawerData2 = new DrawerData2
            {
                Flags = 2,
                ViewModes = 0
            };
            return diskObject;
        }

        public static void SetFirstImage(DiskObject diskObject, ImageData imageData)
        {
            diskObject.Gadget.Width = imageData.Width;
            diskObject.Gadget.Height = imageData.Height;
            diskObject.Gadget.GadgetRenderPointer = 1;
            diskObject.FirstImageData = imageData;
        }

        public static void SetSecondImage(DiskObject diskObject, ImageData imageData)
        {
            diskObject.Gadget.SelectRenderPointer = 1;
            diskObject.SecondImageData = imageData;
        }

        public static DiskObject CreateProjectInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = (ushort)DefaultGadgetActivationFlags;
            diskObject.Gadget.Flags = (ushort)DefaultGadgetFlags;
            diskObject.Type = Constants.DiskObjectTypes.PROJECT;
            return diskObject;
        }

        public static DiskObject CreateDrawerInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = (ushort)DefaultGadgetActivationFlags;
            diskObject.Gadget.Flags = (ushort)DefaultGadgetFlags;
            diskObject.Type = Constants.DiskObjectTypes.DRAWER;
            diskObject.DrawerData = CreateDrawerData(10, 10, 460, 200);
            diskObject.DrawerData.Flags = 33559103;
            diskObject.DrawerDataPointer = 1;
            diskObject.DrawerData2 = new DrawerData2
            {
                Flags = 2,
                ViewModes = 0
            };
            return diskObject;
        }

        public static DiskObject CreateToolInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = (ushort)DefaultGadgetActivationFlags;
            diskObject.Gadget.Flags = (ushort)DefaultGadgetFlags;
            diskObject.Gadget.GadgetId = 0;
            diskObject.Gadget.UserDataPointer = 1;
            diskObject.Type = Constants.DiskObjectTypes.TOOL;
            diskObject.DrawerDataPointer = 0;
            diskObject.DrawerData = null;
            diskObject.DrawerData2 = null;
            return diskObject;
        }

        public static DiskObject CreateGarbageInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = (ushort)DefaultGadgetActivationFlags;
            diskObject.Gadget.Flags = (ushort)DefaultGadgetFlags;
            diskObject.Gadget.GadgetId = 0;
            diskObject.Type = Constants.DiskObjectTypes.GARBAGE;
            diskObject.DrawerData = CreateDrawerData(10, 10, 460, 200);
            diskObject.DrawerData.Flags = 33554687;
            diskObject.DrawerDataPointer = 1;
            diskObject.DrawerData2 = null;
            return diskObject;
        }

        public static IEnumerable<TextData> ConvertStringsToTextDatas(IEnumerable<string> strings)
        {
            return strings.Select(CreateTextData).ToList();
        }
        
        public static IEnumerable<string> ConvertToolTypesToStrings(ToolTypes toolTypes)
        {
            return (toolTypes?.TextDatas ?? new List<TextData>())
                .Select(x => AmigaTextHelper.GetString(x.Data, 0, x.Data.Length - 1)).ToList();
        }

        public static TextData CreateTextData(string text)
        {
            var data = AmigaTextHelper.GetBytes(text).Concat(new byte[] { 0 }).ToArray();
            return new TextData
            {
                Data = data,
                Size = (uint)data.Length
            };
        }

        public static int CalculateDepth(int colors)
        {
            return colors > 1 ? Convert.ToInt32(Math.Ceiling(Math.Log(colors) / Math.Log(2))) : 1;
        }

        public static int CalculateColors(int depth)
        {
            return Convert.ToInt32(Math.Pow(2, depth));
        }

        public static void SetDrawerData2Flags(DiskObject diskObject, DrawerData2.FlagEnum flags)
        {
            if (diskObject.Type != Constants.DiskObjectTypes.DISK && diskObject.Type != Constants.DiskObjectTypes.DRAWER && diskObject.Type != Constants.DiskObjectTypes.GARBAGE)
            {
                return;
            }

            if (diskObject.DrawerData2 == null)
            {
                diskObject.DrawerData2 = new DrawerData2
                {
                    Flags = (uint)(DrawerData2.FlagEnum.ViewIcons | DrawerData2.FlagEnum.AllFiles),
                    ViewModes = (ushort)DrawerData2.ViewModesEnum.ShowIconsOs1X
                };
            }

            diskObject.DrawerData2.Flags = (uint)flags;
        }

        public static void SetDrawerData2ViewMode(DiskObject diskObject, DrawerData2.ViewModesEnum viewMode)
        {
            if (diskObject.Type != Constants.DiskObjectTypes.DISK && diskObject.Type != Constants.DiskObjectTypes.DRAWER && diskObject.Type != Constants.DiskObjectTypes.GARBAGE)
            {
                return;
            }

            if (diskObject.DrawerData2 == null)
            {
                diskObject.DrawerData2 = new DrawerData2
                {
                    Flags = (uint)(DrawerData2.FlagEnum.ViewIcons | DrawerData2.FlagEnum.AllFiles),
                    ViewModes = (ushort)DrawerData2.ViewModesEnum.ShowIconsOs1X
                };
            }

            diskObject.DrawerData2.ViewModes = (ushort)viewMode;
        }

        /// <summary>
        /// From Amiga Developer CD v2.1: The activation should have only RELVERIFY and GADGIMMEDIATE set.
        /// </summary>
        public static Constants.GadgetActivationFlags DefaultGadgetActivationFlags =>
            Constants.GadgetActivationFlags.GactRelverify | Constants.GadgetActivationFlags.GactImmediate;

        public static Constants.GadgetFlags DefaultGadgetFlags => Constants.GadgetFlags.GflgGadgimage;
        
        public static void UpdateGadgetFlags(DiskObject diskObject)
        {
            // by default gadget flag is set to GflgGadgimage, which indicates only 1 planar image is present and
            // it will be shown for render (normal) and select (selected).
            diskObject.Gadget.Flags = (ushort)Constants.GadgetFlags.GflgGadgimage;
            if (diskObject.SecondImageData != null)
            {
                // if second image is present, set gadget flag to GflgGadghimage, which indicates 2 planar images are
                // present and first image will be shown for render (normal) and second image will be shown for select (selected).
                diskObject.Gadget.Flags |= (ushort)Constants.GadgetFlags.GflgGadghimage;
            }
        }

        public static void UpdateDiskObjectBasedOnType(DiskObject diskObject)
        {
            switch (diskObject.Type)
            {
                case Constants.DiskObjectTypes.DISK:
                    diskObject.Gadget.UserDataPointer = 1;
                    diskObject.DrawerDataPointer = 1;
                    diskObject.DrawerData ??= CreateDrawerData(10, 10, 460, 460);
                    diskObject.DrawerData2 ??= new DrawerData2();
                    diskObject.DrawerData.Flags = 33559167;
                    break;
                case Constants.DiskObjectTypes.DRAWER:
                    diskObject.Gadget.UserDataPointer = 1;
                    diskObject.DrawerDataPointer = 1;
                    diskObject.DrawerData ??= CreateDrawerData(10, 10, 460, 460);
                    diskObject.DrawerData2 ??= new DrawerData2();
                    diskObject.DrawerData.Flags = 33559103;
                    break;
                case Constants.DiskObjectTypes.PROJECT:
                case Constants.DiskObjectTypes.TOOL:
                    diskObject.Gadget.UserDataPointer = 1;
                    diskObject.DrawerDataPointer = 0;
                    diskObject.DrawerData = null;
                    diskObject.DrawerData2 = null;
                    break;
                case Constants.DiskObjectTypes.GARBAGE:
                    diskObject.Gadget.UserDataPointer = 1;
                    diskObject.DrawerDataPointer = 1;
                    diskObject.DrawerData ??= CreateDrawerData(10, 10, 460, 460);
                    diskObject.DrawerData2 ??= new DrawerData2();
                    diskObject.DrawerData.Flags = 33554687;
                    break;
            }
        }

        /// <summary>
        /// Read amiga icon from stream.
        /// This is usually a disk object followed by a color icon.
        /// In case it's a PNG file, it will be converted to a disk object and a color icon.
        /// </summary>
        /// <param name="stream">Stream with Amiga icon.</param>
        /// <returns>Amiga icon.</returns>
        public static async Task<AmigaIcon> ReadAmigaIcon(Stream stream)
        {
            stream.Position = 0;

            var magicBytes = await stream.ReadBytes(4);

            if (magicBytes.Length < 4)
            {
                return null;
            }

            stream.Position = 0;

            if (magicBytes.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47}))
            {
                var trueColorIcons = (await TrueColorIconReader.ReadTrueColorIcons(stream)).ToList();
                
                return new AmigaIcon
                {
                    Kind = AmigaIcon.IconKind.TrueColor,
                    DiskObject = GetDiskObject(trueColorIcons),
                    ColorIcon = null,
                    TrueColorIcons = trueColorIcons
                };
            }
            
            var diskObject = await DiskObjectReader.Read(stream);
            var colorIcon = await ColorIconReader.HasColorIcon(stream) 
                ? await ColorIconReader.Read(stream)
                : null;

            return new AmigaIcon
            {
                Kind = AmigaIcon.IconKind.Normal,
                DiskObject = diskObject,
                ColorIcon = colorIcon,
                TrueColorIcons = null
            };
        }

        public static DiskObject GetDiskObject(IEnumerable<TrueColorIcon> trueColorIcons)
        {
            if (trueColorIcons == null)
            {
                return null;
            }

            var trueColorIconsList = trueColorIcons.ToList();
            
            var trueColorIcon = trueColorIconsList.FirstOrDefault();
            
            if (trueColorIcon == null)
            {
                return null;
            }

            var iHdrChunk = trueColorIconsList.SelectMany(x => x.Chunks)
                .FirstOrDefault(c => c.Type.SequenceEqual(TrueColorIcons.Constants.PngChunkTypes.Ihdr));
            
            if (iHdrChunk == null)
            {
                return null;
            }
            
            var pngHeader = TrueColorIconReader.ReadPngHeader(iHdrChunk.Data);
            
            var iconChunk = trueColorIconsList.SelectMany(x => x.Chunks)
                .FirstOrDefault(c => c.Type.SequenceEqual(TrueColorIcons.Constants.PngChunkTypes.Icon));
            
            if (iconChunk == null)
            {
                return null;
            }
            
            var iconData = TrueColorIconReader.ReadIconData(iconChunk.Data);

            var diskObject = CreateProjectInfo();
            
            diskObject.Gadget.Width = (short)pngHeader.Width;
            diskObject.Gadget.Height = (short)pngHeader.Height;

            if (!string.IsNullOrEmpty(iconData.DefaultTool))
            {
                diskObject.DefaultTool = CreateTextData(iconData.DefaultTool);
                diskObject.DefaultToolPointer = 1;
            }

            if (!string.IsNullOrEmpty(iconData.ToolType))
            {
                var toolTypes = iconData.ToolType.Split(new []{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);

                diskObject.ToolTypes = new ToolTypes
                {
                    TextDatas = ConvertStringsToTextDatas(toolTypes)
                };
                diskObject.ToolTypesPointer = 1;
            }
            
            var typeTag = iconData.IconAttributeTags.FirstOrDefault(t => t.Tag == TrueColorIcons.Constants.IconAttributeTags.ATTR_TYPE);
            if (typeTag != null)
            {
                diskObject.Type = (byte)typeTag.Value;
                UpdateDiskObjectBasedOnType(diskObject);
            }

            foreach (var iconAttributeTag in iconData.IconAttributeTags)
            {
                switch (iconAttributeTag.Tag)
                {
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_ICONX:
                        diskObject.CurrentX = (int)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_ICONY:
                        diskObject.CurrentY = (int)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_STACKSIZE:
                        diskObject.StackSize = (int)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DRAWERWIDTH:
                        diskObject.DrawerData.Width = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DRAWERHEIGHT:
                        diskObject.DrawerData.Height = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DRAWERX:
                        diskObject.DrawerData.LeftEdge = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DRAWERY:
                        diskObject.DrawerData.TopEdge = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DD_CURRENTX:
                        diskObject.DrawerData.CurrentX = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DD_CURRENTY:
                        diskObject.DrawerData.CurrentY = (short)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_DRAWERFLAGS:
                        diskObject.DrawerData2.Flags = (ushort)iconAttributeTag.Value;
                        break;
                    case TrueColorIcons.Constants.IconAttributeTags.ATTR_VIEWMODES:
                        diskObject.DrawerData2.ViewModes = (ushort)iconAttributeTag.Value;
                        break;
                }
            }
            
            return diskObject;
        }
    }
}