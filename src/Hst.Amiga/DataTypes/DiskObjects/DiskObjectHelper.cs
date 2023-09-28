namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amiga;
    using Core;

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
                    Activation = 1,
                    Flags = 6,
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
            diskObject.Gadget.Activation = 1;
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
            diskObject.Gadget.Activation = 1;
            diskObject.Gadget.Flags = 6;
            diskObject.Type = Constants.DiskObjectTypes.PROJECT;
            return diskObject;
        }

        public static DiskObject CreateDrawerInfo()
        {
            var diskObject = CreateInfo();
            diskObject.Gadget.Activation = 1;
            diskObject.Gadget.Flags = 6;
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
            diskObject.Gadget.Activation = 1;
            diskObject.Gadget.Flags = 6;
            diskObject.Gadget.GadgetId = 100;
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
            diskObject.Gadget.Activation = 3;
            diskObject.Gadget.Flags = 6;
            diskObject.Gadget.GadgetId = 0;
            diskObject.Type = Constants.DiskObjectTypes.GARBAGE;
            diskObject.DrawerDataPointer = 0;
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
    }
}