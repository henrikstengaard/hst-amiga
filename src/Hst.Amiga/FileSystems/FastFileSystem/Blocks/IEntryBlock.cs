namespace Hst.Amiga.FileSystems.FastFileSystem.Blocks
{
    using System;

    public interface IEntryBlock
    {
        uint Access { get; set; } // 0x140: file header block
        uint ByteSize { get; set; } // 0x144: file header block
        string Comment { get; set; } // 0x148: length, 0x149: comment
        DateTime Date { get; set; } // 0x1a4: days, 0x1a8: mins, 0x1ac: ticks
        string Name { get; set; } // 0x1b0: length, 0x1b1: name
    }
}