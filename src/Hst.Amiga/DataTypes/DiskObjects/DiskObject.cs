﻿namespace Hst.Amiga.DataTypes.DiskObjects
{
    public class DiskObject
    {
        public ushort Magic { get; set; }
        public ushort Version { get; set; }
        public Gadget Gadget { get; set; }
        public byte Type { get; set; }
        public byte Pad { get; set; }
        public uint DefaultToolPointer { get; set; }
        public TextData DefaultTool { get; set; }
        public uint ToolTypesPointer { get; set; }
        public ToolTypes ToolTypes { get; set; }
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }
        public uint DrawerDataPointer { get; set; }
        public DrawerData DrawerData { get; set; }
        public ImageData FirstImageData { get; set; }
        public ImageData SecondImageData { get; set; }
        public uint ToolWindowPointer { get; set; }
        public int StackSize { get; set; }
        public DrawerData2 DrawerData2 { get; set; }

        public DiskObject()
        {
            Magic = 0xe310;
            Version = 1;
        }
    }
}