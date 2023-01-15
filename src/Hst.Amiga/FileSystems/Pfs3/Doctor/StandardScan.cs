namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Converters;

    public static class StandardScan
    {
        public static async Task Check(Stream stream)
        {
            var g = new globaldata
            {
                fnsize = 107,
                RootBlock = new RootBlock
                {
                    ReservedBlksize = 1024
                }
            };
            
            var dirBlocks = new List<CachedBlock>();
            var anodeBlocks = new List<CachedBlock>();

            var buffer = new byte[1024];
            uint? currentBlockNr = null;
            bool endOfStream;
            do
            {
                var blockNr = currentBlockNr;
                var bytesRead = await stream.ReadAsync(buffer, 0, 512);
                endOfStream = bytesRead != 512;

                var blockId = BigEndianConverter.ConvertBytesToUInt16(buffer);
                var rootId = BigEndianConverter.ConvertBytesToInt32(buffer);

                if (rootId == Constants.ID_PFS_DISK && !currentBlockNr.HasValue)
                {
                    currentBlockNr = 0;
                }

                switch (blockId)
                {
                    case Constants.ABLKID:
                        bytesRead = await stream.ReadAsync(buffer, 512, 512);
                        endOfStream = bytesRead != 512;
                        
                        if (currentBlockNr.HasValue)
                        {
                            currentBlockNr++;
                        }

                        var anodeBlock = await AnodeBlockReader.Parse(buffer, g);
                        anodeBlocks.Add(new CachedBlock
                        {
                            blocknr = blockNr ?? uint.MaxValue,
                            blk = anodeBlock
                        });
                        break;
                    case Constants.DBLKID:
                        bytesRead = await stream.ReadAsync(buffer, 512, 512);
                        endOfStream = bytesRead != 512;

                        if (currentBlockNr.HasValue)
                        {
                            currentBlockNr++;
                        }

                        var dirBlock = await DirBlockReader.Parse(buffer, g);
                        dirBlocks.Add(new CachedBlock
                        {
                            blocknr = blockNr ?? uint.MaxValue,
                            blk = dirBlock
                        });
                        break;
                }

                if (currentBlockNr.HasValue)
                {
                    currentBlockNr++;
                }
            } while (!endOfStream);


            foreach (var dirBlock in dirBlocks)
            {
                var blk = dirBlock.dirblock;
                var dirEntries = new List<direntry>();
                direntry dirEntry;
                var offset = 0;
                do
                {
                    dirEntry = DirEntryReader.Read(blk.entries, offset);
                    if (dirEntry.next == 0)
                    {
                        break;
                    }
                    
                    dirEntries.Add(dirEntry);
                    offset += dirEntry.next;
                } while (offset + dirEntry.next < blk.entries.Length);

                foreach (var de in dirEntries)
                {
                    // if (de->next & 1)
                    if ((de.next & 1) != 0)
                    {
                        throw new IOException("Odd directory entry length");
                    }
                    
                    // if (de->nlength + offsetof(struct direntry, nlength) > de->next)
                    if (de.nlength + de.startofname > de.next)
                    {
                        throw new IOException("Invalid filename");
                    }
                    
                    // if (de->nlength > volume.fnsize)
                    if (de.nlength > g.fnsize)
                    {
                        throw new IOException("Filename too long");
                    }

                    if (de.Offset + de.startofname + de.nlength >= blk.entries.Length)
                    {
                        
                    }
                    
                    // if (*FILENOTE(de) + de->nlength + offsetof(struct direntry, nlength) > de->next)
                    var fileNoteLength = blk.entries[de.Offset + de.nlength + de.startofname];
                    if (de.startofname + de.nlength + fileNoteLength > de.next)
                    {
                        throw new IOException("Invalid filenote");
                    }

                    var anodeBlock = anodeBlocks.FirstOrDefault(x => x.blocknr == de.anode);
                    switch (de.type)
                    {
                        case Constants.ST_USERDIR:
                            RepairDir(de, dirBlock);
                            break;
                        case Constants.ST_FILE:
                            if (anodeBlock == null)
                            {
                                continue;
                            }
                            RepairFile(de, dirBlock, anodeBlock);
                            break;
                    }
                }
            }

        }
        
        private static void RepairDir(direntry dirEntry, CachedBlock dirblock)
        {
            
        }
    
        private static void RepairFile(direntry dirEntry, CachedBlock dirblock, CachedBlock anodeblock)
        {
            anode filenode;
            var b = anodeblock.ANodeBlock;
        
        
            // /* bitmap gen */
            // for (var anodenr = dirEntry.anode; anodenr > 0; anodenr = filenode.next)
            // {
            //     GetAnode(filenode, anodenr, true);
            //     if ((error = AnodeUsed(anodenr)))
            //         break;
            //
            //     for (bl = filenode.blocknr; bl < filenode.blocknr + filenode.clustersize; bl++)
            //         if ((error = MainBlockUsed(bl)))
            //             break;
            // }
            //     
        }
        
    }
}