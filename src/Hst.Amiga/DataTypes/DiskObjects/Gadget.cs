namespace Hst.Amiga.DataTypes.DiskObjects
{
    public class Gadget
    {
        public uint NextPointer { get; set; }
        public short LeftEdge { get; set; }
        public short TopEdge { get; set; }
        public short Width { get; set; }
        public short Height { get; set; }
        public ushort Flags { get; set; }
        public ushort Activation { get; set; }
        public ushort GadgetType { get; set; }

        /// <summary>
        /// pointer to gadget render, first image.
        /// (Set this to an appropriate Image structure.)
        /// </summary>
        public uint GadgetRenderPointer { get; set; }

        /// <summary>
        /// pointer to select render, second image. if zero, second image is not defined.
        /// Set this to an appropriate alternate Image structure if and only if the highlight mode is GADGHIMAGE.
        /// </summary>
        public uint SelectRenderPointer { get; set; }
        
        public uint GadgetTextPointer { get; set; }
        public int MutualExclude { get; set; }
        public uint SpecialInfoPointer { get; set; }
        public ushort GadgetId { get; set; }
        public uint UserDataPointer { get; set; }
    }
}