namespace Hst.Amiga.Tests.FastFileSystemTests
{
    using System.Linq;
    using FileSystems.FastFileSystem.Blocks;
    using Xunit;

    public class GivenDirBlock
    {
        [Fact]
        public void WhenReadAsEntryBlockThenBlocksAreEqual()
        {
            var dirBlock = new DirBlock
            {
                HeaderKey = 887,
                HighSeq = 0,
                HashTableSize = 72,
                HashTable = Enumerable.Range(1, 72).ToArray(),
                Comment = "Comment",
                Name = "DirEntry",
                Parent = 880,
            };

            var dirBlockBytes = EntryBlockBuilder.Build(dirBlock, 512);

            System.IO.File.WriteAllBytes("dir-block.bin", dirBlockBytes);
            
            var entryBlock = EntryBlockParser.Parse(dirBlockBytes);
            
            Assert.Equal(dirBlock.Type, entryBlock.Type);
            Assert.Equal(dirBlock.HeaderKey, entryBlock.HeaderKey);
            Assert.Equal(dirBlock.HighSeq, entryBlock.HighSeq);
            Assert.Equal(dirBlock.HashTableSize, entryBlock.HashTableSize);
            Assert.Equal(dirBlock.HashTable, entryBlock.HashTable);
            Assert.Equal(dirBlock.Comment, entryBlock.Comment);
            Assert.Equal(dirBlock.Name, entryBlock.Name);
            Assert.Equal(dirBlock.Parent, entryBlock.Parent);
            Assert.Equal(dirBlock.SecType, entryBlock.SecType);
        }
    }
}