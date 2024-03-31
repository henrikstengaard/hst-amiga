using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Hst.Core.Converters;

namespace Hst.Amiga.DataTypes.Iffs
{
    public class Chunk
    {
        private readonly List<byte> chunkData;

        public IReadOnlyList<byte> Data;

        public readonly string Id;

        public Chunk(string id)
        {
            Id = id;
            chunkData = new List<byte>();
            Data = new ReadOnlyCollection<byte>(chunkData);
        }

        public void AddData(byte data)
        {
            chunkData.Add(data);
        }

        public void AddData(byte[] data)
        {
            chunkData.AddRange(data);
        }

        public byte[] GetChunkData()
        {
            var zeroPad = (chunkData.Count & 1) == 1;
            var length = zeroPad ? chunkData.Count + 1 : chunkData.Count;
            var data = zeroPad ? chunkData.Concat(new byte[1]) : chunkData;

            return Encoding.ASCII.GetBytes(Id)
                .Concat(BigEndianConverter.ConvertUInt32ToBytes((uint)length))
                .Concat(data).ToArray();
        }
    }
}