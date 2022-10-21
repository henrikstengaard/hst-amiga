namespace Hst.Amiga.Tests.Pfs3Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Converters;
using Core.Extensions;
using FileSystems.FastFileSystem;
using FileSystems.Pfs3;
using FileSystems.Pfs3.Blocks;
using RigidDiskBlocks;
using Xunit;
using Constants = FileSystems.Pfs3.Constants;
using Directory = FileSystems.Pfs3.Directory;
using Disk = FileSystems.FastFileSystem.Disk;
using FileMode = System.IO.FileMode;
using Volume = FileSystems.Pfs3.Volume;

public class CanSalvage
{
    public class Block
    {
        public uint BlockNr { get; set; }
        public byte[] Bytes { get; set; }
        public ushort Id { get; set; }
    }
    
    [Fact(Skip = "Experimental pfs3 salvage test")]
    public async Task Salvage()
    {
        await using var stream = System.IO.File.Open(@"d:\hst-imager\dh2.hdf", FileMode.Open, FileAccess.ReadWrite);

        var g = CreateGlobalData(stream.Length);
        g.stream = stream;

        Init.Initialize(g);

        var rootBlock = await Volume.GetCurrentRoot(g);
        rootBlock.Extension = 0; // fake no extension

        //await Volume.DiskInsertSequence(rootBlock, g);
        g.currentvolume = await Volume.MakeVolumeData(rootBlock, g);

        /* update rootblock */
        g.RootBlock = g.currentvolume.rootblk = rootBlock;

        // minimal init anodes
        var volume = g.currentvolume;
        var andata = g.glob_anodedata;
        andata.curranseqnr =
            (ushort)(volume.rblkextension != null ? volume.rblkextension.rblkextension.curranseqnr : 0);
        //andata.anodesperblock = (volume->rootblk->reserved_blksize - sizeof(anodeblock_t)) / sizeof(anode_t);
        //andata.indexperblock = (volume->rootblk->reserved_blksize - sizeof(indexblock_t)) / sizeof(LONG);
        andata.anodesperblock = (ushort)((volume.rootblk.ReservedBlksize - SizeOf.ANODEBLOCK_T) / SizeOf.ANODE_T);
        andata.indexperblock = (ushort)((volume.rootblk.ReservedBlksize - SizeOf.INDEXBLOCK_T) / 4);
        andata.maxanodeseqnr = (uint)(g.SuperMode
            ? ((Constants.MAXSUPER + 1) * andata.indexperblock * andata.indexperblock * andata.anodesperblock - 1)
            : (Constants.MAXSMALLINDEXNR * andata.indexperblock - 1));
        andata.reserved = (ushort)(andata.anodesperblock - Constants.RESERVEDANODES);
        /* Reconfigure modules to new volume */
        //await Init.InitModules (g.currentvolume, false, g);

        /* create rootblockextension if its not there yet */
        // if (g.currentvolume.rblkextension == null &&
        //     g.diskstate != Constants.ID_WRITE_PROTECTED)
        // {
        //     Pfs3Formatter.MakeRBlkExtension (g);
        // }        


        // var g = new globaldata
        // {
        //     RootBlock = new RootBlock
        //     {
        //         ReservedBlksize = 1024
        //     }
        // };
        //
        var blocks = new Dictionary<uint, Block>();
        var dirBlocks = new List<dirblock>();
        var anodeBlocks = new List<anodeblock>();

        var bufferLength = 1024 * 1024;
        var buffer = new byte[bufferLength];
        int bytesRead;
        var pos = 0L;
        do
        {
            pos = stream.Length;
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            for (var i = 0; i < bytesRead; i += 512)
            {
                var id = BigEndianConverter.ConvertBytesToUInt16(buffer, i);

                if (i + g.RootBlock.ReservedBlksize > buffer.Length)
                {
                    var extra = new byte[buffer.Length - i];
                    var extraBytesRead = await stream.ReadAsync(extra, 0, extra.Length);
                    if (extraBytesRead != extra.Length)
                    {
                        
                    }
                    buffer = buffer.Concat(extra).ToArray();
                }
                
                switch (id)
                {
                    case Constants.DBLKID:
                    case Constants.ABLKID:
                    case Constants.IBLKID:
                        // try
                        // {
                        //     var anodeBlock =
                        //         await AnodeBlockReader.Parse(buffer.Skip(i).Take(g.RootBlock.ReservedBlksize).ToArray(), g);
                        //     anodeBlocks.Add(anodeBlock);
                        //
                        // }
                        // catch (Exception e)
                        // {
                        //     throw;
                        // }
                        var blockNo = (uint)((pos + i) / 512);
                        blocks[blockNo] = new Block
                        {
                            BlockNr = blockNo,
                            Bytes = buffer.Skip(i).Take(g.RootBlock.ReservedBlksize).ToArray(),
                            Id = id
                        };
                        break;
                }
            }
        } while (bytesRead == bufferLength);

        // var dirBlock =
        //     await DirBlockReader.Parse(buffer.Skip(i).Take(g.RootBlock.ReservedBlksize).ToArray(), g);
        // dirBlocks.Add(dirBlock);

        await ExtractDirEntries(@"d:\hst-imager\dh2", Constants.ANODE_ROOTDIR, blocks, g);

        // var rootDirBlocks = dirBlocks.Where(x => x.parent == Constants.ANODE_ROOTDIR).ToList();
        //
        // var rootEntries = rootDirBlocks.SelectMany(ReadDirEntries).OrderBy(x => x.Name).ToList();
        //
        // var t = 0;

        //
        // stream.Seek(FileSystems.Pfs3.Constants.ROOTBLOCK * 512, SeekOrigin.Begin);
        // var blockBytes = await stream.ReadBytes(512);
        // var rootBlock = await RootBlockReader.Parse(blockBytes);
        // rootBlock.Extension = extension;
        //
        // stream.Seek(FileSystems.Pfs3.Constants.ROOTBLOCK * 512, SeekOrigin.Begin);
        // blockBytes = await RootBlockWriter.BuildBlock(rootBlock);
        // await stream.WriteBytes(blockBytes);
        //
        // var rigidDiskBlock = RigidDiskBlock.Create(20.GB());
        // var size = stream.Length;
        // var blocksPerCylinder = rigidDiskBlock.Heads * rigidDiskBlock.Sectors;
        // var cylinders = (uint)Math.Floor((double)size / (blocksPerCylinder * rigidDiskBlock.BlockSize));
        // var lowCyl = 0U;
        // var highCyl = cylinders - 1 > rigidDiskBlock.HiCylinder
        //     ? rigidDiskBlock.HiCylinder
        //     : cylinders - 1;
        // var partitionSize = (long)(highCyl - lowCyl + 1) * rigidDiskBlock.Heads * rigidDiskBlock.Sectors *
        //                     rigidDiskBlock.BlockSize;
        //
        // var p = new PartitionBlock
        // {
        //     LowCyl = lowCyl,
        //     HighCyl = highCyl,
        //     PartitionSize = partitionSize
        // };
        //
        // //var partitionBlock = PartitionBlock.Create(rigidDiskBlock, DosTypeHelper.FormatDosType("PFS3"), "DH2", stream.Length, false);
        // await using var pfs3Volume = await Pfs3Volume.Mount(stream, p);
        // var entries = (await pfs3Volume.ListEntries()).ToList();
        //
    }

    private globaldata CreateGlobalData(long size)
    {
        const uint blockSize = 512U;
        var sectors = (uint)(size / blockSize);
        const uint blocksPerTrack = 63U;
        const uint surfaces = 16U;
        var blocksPerCylinder = surfaces * blocksPerTrack;
        var cylinders = (uint)Math.Floor((double)size / (blocksPerCylinder * blockSize));
        const uint lowCyl = 0U;
        var highCyl = cylinders - 1;
        //var partitionSize = (long)(highCyl - lowCyl + 1) * rigidDiskBlock.Heads * rigidDiskBlock.Sectors *rigidDiskBlock.BlockSize;
        const uint mask = 2147483646U;

        return Init.CreateGlobalData(sectors, blocksPerTrack, surfaces, lowCyl, highCyl, 30, blockSize, mask);
    }

    private async Task ExtractDirEntries(string path, uint anodenr, Dictionary<uint, Block> blocks, globaldata g)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }

        var dirBlocks = new Dictionary<uint, dirblock>();

        foreach (var block in blocks.Where(x => x.Value.Id == Constants.DBLKID))
        {
            dirBlocks[block.Key] = await DirBlockReader.Parse(block.Value.Bytes, g);
        }

        var anodeBlocks = new Dictionary<uint, anodeblock>();

        foreach (var block in blocks.Where(x => x.Value.Id == Constants.ABLKID))
        {
            anodeBlocks[block.Key] = await AnodeBlockReader.Parse(block.Value.Bytes, g);
        }

        var indexBlocks = new Dictionary<uint, indexblock>();

        foreach (var block in blocks.Where(x => x.Value.Id == Constants.IBLKID))
        {
            indexBlocks[block.Key] = await IndexBlockReader.Parse(block.Value.Bytes, g);
        }

        
        //var dirEntries = dirBlocks.Where(x => x.parent == anodenr).SelectMany(ReadDirEntries).OrderBy(x => x.Name).ToList();

        foreach (var dirBlock in dirBlocks.Where(x => x.Value.parent == anodenr))
        {
            var dirEntries = ReadDirEntries(dirBlock.Value).OrderBy(x => x.Name).ToList();

            foreach (var dirEntry in dirEntries)
            {
                if (dirEntry.type != Constants.ST_FILE)
                {
                    continue;
                }

                // get anode
                var temp		 = Init.divide(dirEntry.anode, g.glob_anodedata.anodesperblock);
                var seqnr        = (ushort)temp;				// 1e block = 0
                var anodeoffset  = (ushort)(temp >> 16);

                // big_GetAnodeBlock
                var nr = Init.divide(seqnr, g.glob_anodedata.indexperblock);
                var blocknr = g.currentvolume.rootblk.idx.small.indexblocks[nr];                
                
                //await CreateListEntry(dirEntry.anode, g);
                var anode = anodeBlocks.Where(x => x.Value.seqnr == seqnr).ToList();
                
                // 1. Directory.Find
                var objectInfo = new objectinfo
                {
                    file = new fileinfo
                    {
                        direntry = dirEntry,
                        dirblock = new CachedBlock(g)
                        {
                            blk = dirBlock.Value
                        }
                    },
                    volume = new volumeinfo
                    {
                        root = 1, // not root
                        volume = g.currentvolume
                    }
                };

                // 2. Open, MakeListEntry
                var type = new ListType
                {
                    value = Constants.ET_FILEENTRY
                };

                var fileentry = new fileentry
                {
                    le = new listentry
                    {
                        info = objectInfo,
                        volume = g.currentvolume
                    },
                };

                var fileEntryX = await FileSystems.Pfs3.File.Open(objectInfo, false, g) as fileentry;
                //return new EntryStream(fileEntry, g);

                var buffer = new byte[dirEntry.fsize];
                await FileSystems.Pfs3.Disk.ReadFromFile(fileentry, buffer, (uint)buffer.Length, g);

                var entryPath = Path.Combine(path, dirEntry.Name);
                await using var s = System.IO.File.Open(entryPath, FileMode.Create);
            }
        }
    }

    private async Task CreateListEntry(uint anodenr, globaldata g)
    {
        await anodes.GetAnodeChain(anodenr, g);        
        
        // var fileentry = new fileentry
        // {
        //     le = listentry
        // };
        // listentry.filelock.fl_Key = (int)listentry.anodenr;
        // // listentry->lock.fl_Volume = MKBADDR(MKBADDR(newinfo.file.dirblock->volume->devlist);
        // listentry.volume = newinfo.file.dirblock.volume;
        // fileentry.originalsize = Macro.IsDelFile(newinfo)
        //     ? Directory.GetDDFileSize(dde, g)
        //     : Directory.GetDEFileSize(newinfo.file.direntry, g);
        //
        // /* Get anodechain. If it fails anodechain will become NULL. This has to be
        //  * taken into account by functions that use the chain
        //  */
        // fileentry.anodechain = await anodes.GetAnodeChain(listentry.anodenr, g);
        // fileentry.currnode = fileentry.anodechain.head;
        
    }

    private IEnumerable<direntry> ReadDirEntries(dirblock dirBlock)
    {
        var dirEntryIndex = 0;
        direntry dirEntry;
        do
        {
            dirEntry = DirEntryReader.Read(dirBlock.entries, dirEntryIndex);
            if (dirEntry.next != 0)
            {
                yield return dirEntry;
            }

            dirEntryIndex += dirEntry.next;
        } while (dirEntry.next != 0 && dirEntryIndex + dirEntry.next < dirBlock.entries.Length);
    }
}