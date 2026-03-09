namespace Hst.Amiga.DataTypes.DiskObjects
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
        
        /// <summary>
        /// Drawer data pointer indicates if drawer data is present. If drawer data pointer is 0, no drawer data is present. If drawer data pointer is not 0, drawer data is present and can be read from the stream.
        /// </summary>
        public uint DrawerDataPointer { get; set; }
        
        public DrawerData DrawerData { get; set; }
        public ImageData FirstImageData { get; set; }
        public ImageData SecondImageData { get; set; }
        public uint ToolWindowPointer { get; set; }
        public int StackSize { get; set; }
        /// <summary>
        /// DrawerData2 structure for OS2.x drawers.
        /// Drawer data 2 requires drawer data pointer is not 0 and user data pointer is 1.
        /// </summary>
        public DrawerData2 DrawerData2 { get; set; }

        public DiskObject()
        {
            Magic = 0xe310;
            Version = 1;
        }
    }
}