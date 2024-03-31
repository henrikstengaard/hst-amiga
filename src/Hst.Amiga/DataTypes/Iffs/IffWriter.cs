using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;

namespace Hst.Amiga.DataTypes.Iffs
{
    public class IffWriter
    {
        private readonly Stream stream;
        private readonly Stack<Chunk> chunks;
        private Chunk currentChunk;

        public IffWriter(Stream stream)
        {
            this.stream = stream;
            chunks = new Stack<Chunk>();
            currentChunk = null;
        }

        public Chunk BeginChunk(string id)
        {
            currentChunk = new Chunk(id);
            chunks.Push(currentChunk);
            return currentChunk;
        }

        public async Task EndChunk()
        {
            if (chunks.Count == 0)
            {
                throw new IndexOutOfRangeException("No chunks to end");
            }
            
            var completedChunk = chunks.Pop();
            var chunkData = completedChunk.GetChunkData();

            if (chunks.Count == 0)
            {
                await stream.WriteBytes(chunkData);
                return;
            }
            
            currentChunk = chunks.Peek();
            
            currentChunk.AddData(chunkData);
        }
    }
}