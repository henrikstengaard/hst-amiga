namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class Crc32
    {
        private const uint InitialValue = 0xFFFFFFFFU;
        private const uint FinalXor = 0xFFFFFFFFU;
        
        /// <summary>
        /// IEEE 802.3 CRC-32 polynomial.
        /// </summary>
        private const uint Crc32Polynomial = 0xEDB88320U;
        
        private class Table
        {
            public readonly uint[] Values;

            public Table()
            {
                Values = new uint[256];

                for (uint i = 0; i < 256; i++)
                {
                    var c = i;
                    for (var j = 0; j < 8; j++)
                    {
                        if ((c & 1) != 0)
                        {
                            c = Crc32Polynomial ^ (c >> 1);
                        }
                        else
                        {
                            c >>= 1;
                        }
                    }
                    Values[i] = c;
                }
            }
        }

        private uint crc = InitialValue;
        
        private static readonly Table Crc32Table = new Table();
        
        public void Compute(byte[] data, int offset, int count)
        {
            for (var i = offset; i < offset + count; i++)
            {
                crc = Crc32Table.Values[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
        }

        public void Compute(byte[] data)
        {
            Compute(data, 0, data.Length);
        }
        
        public uint GetCalculatedCrc() => crc ^ FinalXor;
    }
}