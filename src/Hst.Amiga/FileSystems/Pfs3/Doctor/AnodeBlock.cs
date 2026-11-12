namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Anode block view (anodeblock_t in pfs3.h).
    /// </summary>
    public class AnodeBlock : StructView
    {
        public AnodeBlock(byte[] data) : base(data, 0)
        {
        }

        /// <summary>
        /// 'AB'
        /// </summary>
        public ushort Id
        {
            get => GetUInt16(0);
            set => SetUInt16(0, value);
        }

        public uint Datestamp
        {
            get => GetUInt32(4);
            set => SetUInt32(4, value);
        }

        public uint SeqNr
        {
            get => GetUInt32(8);
            set => SetUInt32(8, value);
        }

        /// <summary>
        /// nodes[i]
        /// </summary>
        public Anode Nodes(uint i) => new Anode(Data, Offset + 16 + (int)i * Anode.SizeOf);
    }

    /// <summary>
    /// Anode view (anode_t in pfs3.h).
    /// </summary>
    public class Anode : StructView
    {
        /// <summary>
        /// sizeof(anode_t)
        /// </summary>
        public const int SizeOf = 12;

        public Anode(byte[] data, int offset) : base(data, offset)
        {
        }

        public uint ClusterSize
        {
            get => GetUInt32(0);
            set => SetUInt32(0, value);
        }

        public uint BlockNr
        {
            get => GetUInt32(4);
            set => SetUInt32(4, value);
        }

        public uint Next
        {
            get => GetUInt32(8);
            set => SetUInt32(8, value);
        }
    }
}
