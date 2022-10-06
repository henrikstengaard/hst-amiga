﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;
    using Core.Extensions;

    public static class Disk
    {
        // disk.c
        
        public static void BoundsCheck(bool write, uint blocknr, uint blocks, globaldata g)
        {
            if(!(Macro.InPartition(blocknr, g) && Macro.InPartition(blocknr+blocks-1, g)))
            {
                // ULONG args[5];
                // args[0] = g->tdmode;
                // args[1] = blocknr;
                // args[2] = blocks;
                // args[3] = g->firstblock;
                // args[4] = g->lastblock;
                // ErrorMsg(write ? AFS_ERROR_WRITE_OUTSIDE : AFS_ERROR_READ_OUTSIDE, args, g);
                throw new IOException(write ? "AFS_ERROR_WRITE_OUTSIDE" : "AFS_ERROR_READ_OUTSIDE");
            }
        }

        public static async Task<byte[]> RawRead(uint blocks, uint blocknr, globaldata g)
        {
            if (blocknr == UInt32.MaxValue) // blocknr of uninitialised anode
            {
                return default;
            }

            blocknr += g.firstblock;

            if (g.softprotect)
            {
                throw new IOException("ERROR_DISK_WRITE_PROTECTED");
            }

            BoundsCheck(false, blocknr, blocks, g);
            
            // seek to block in stream
            // while (blocks > 0)
            // {
            //     var transfer = min(blocks,maxtransfer);
            //     
            //     buffer += transfer << BLOCKSHIFT;
            //     blocks -= transfer;
            //     blocknr += transfer;                
            // }

            var offset = (long)g.blocksize * blocknr;
            g.stream.Seek(offset, SeekOrigin.Begin);

            // read block bytes
            return await g.stream.ReadBytes((int)(g.blocksize * blocks));
        }

        public static async Task<IBlock> RawRead<T>(uint blocks, uint blocknr, globaldata g) where T : IBlock
        {
            var buffer = await RawRead(blocks, blocknr, g);

            var type = typeof(T);
            if (type == typeof(anodeblock))
            {
                return await AnodeBlockReader.Parse(buffer, g);
            }

            if (type == typeof(dirblock))
            {
                return await DirBlockReader.Parse(buffer, g);
            }
            
            if (type == typeof(indexblock))
            {
                return await IndexBlockReader.Parse(buffer, g);
            }

            if (type == typeof(BitmapBlock))
            {
                return await BitmapBlockReader.Parse(buffer, (int)g.glob_allocdata.longsperbmb);
            }

            if (type == typeof(deldirblock))
            {
                return await DelDirBlockReader.Parse(buffer, g);
            }

            if (type == typeof(rootblockextension))
            {
                return await RootBlockExtensionReader.Parse(buffer);
            }
            
            return default;
        }

        public static async Task<bool> RawWrite(Stream stream, byte[] buffer, uint blocks, uint blocknr, globaldata g)
        {
            // RawReadWrite_DS(TRUE, buffer, blocks, blocknr, g);
            
            if(blocknr == UInt32.MaxValue)   // blocknr of uninitialised anode
                return false;

            blocknr += g.firstblock;

            if (g.softprotect)
            {
                throw new IOException("ERROR_DISK_WRITE_PROTECTED");
            }

            BoundsCheck(true, blocknr, blocks, g);
            
            var offset = (long)g.blocksize * blocknr;
            g.stream.Seek(offset, SeekOrigin.Begin);
            
            await stream.WriteBytes(buffer);

            return true;
        }

        public static async Task<bool> RawWrite(Stream stream, IBlock block, uint blocks, uint blocknr, globaldata g)
        {
            byte[] buffer;
            switch (block)
            {
                case anodeblock anodeBlock:
                    buffer = await AnodeBlockWriter.BuildBlock(anodeBlock);
                    break;
                case dirblock dirBlock:
                    buffer = await DirBlockWriter.BuildBlock(dirBlock);
                    break;
                case indexblock indexBlock:
                    buffer = await IndexBlockWriter.BuildBlock(indexBlock);
                    break;
                case BitmapBlock bitmapBlock:
                    buffer = await BitmapBlockWriter.BuildBlock(bitmapBlock);
                    break;
                case deldirblock deldirblock:
                    buffer = await DelDirBlockWriter.BuildBlock(deldirblock);
                    break;
                case rootblockextension rootBlockExtension:
                    buffer = await RootBlockExtensionWriter.BuildBlock(rootBlockExtension);
                    break;
                default:
                    return false;
            }

            return await RawWrite(stream, buffer, blocks, blocknr, g);
        }
        
/* write all dirty blocks to disk
 */
        public static async Task UpdateDataCache(globaldata g)
        {
            int i;

            for (i = 0; i < g.dc.size; i++)
            {
                if (g.dc.ref_[i].dirty && g.dc.ref_[i].blocknr != 0)
                    await UpdateSlot(i, g);
            }
        }
        
        
/* update a data cache slot, and any adjacent blocks
 */
        public static Task UpdateSlot(int slotnr, globaldata g)
        {
            uint blocknr;
            int i;
	
            blocknr = g.dc.ref_[slotnr].blocknr;

            /* find out how many adjacent blocks can be written */
            for (i=slotnr; i< g.dc.size; i++)
            {
                if (g.dc.ref_[i].blocknr != blocknr++)
                    break;
                g.dc.ref_[i].dirty = false;
            }

            /* write them */
            //await RawWrite(g.dc.data[slotnr << g.blockshift], i-slotnr, g.dc.ref_[slotnr].blocknr, g);
            return Task.CompletedTask;
        }
        
        /* SeekInFile
**
** Specification:
**
** - set fileposition
** - if wrong position, resultposition unknown and error
** - result = old position to start of file, -1 = error
**
** - the end of the file is 0 from end
*/
        public static async Task<int> SeekInFile(fileentry file, int offset, int mode, globaldata g)
        {
            int oldoffset, newoffset;
            uint anodeoffset, blockoffset;
            // #if DELDIR
            deldirentry delfile = null;

	        // DB(Trace(1,"SeekInFile","offset = %ld mode=%ld\n",offset,mode));
            if (Macro.IsDelFile(file.le.info))
            {
                if ((delfile = await Directory.GetDeldirEntryQuick(file.le.info.delfile.slotnr, g)) == null)
                    return -1;
            }
            // #endif

            /* do the seeking */
            oldoffset = (int)file.offset;
            newoffset = -1;

            /* TODO: 32-bit wraparound checks */

            switch (mode)
            {
                case Constants.OFFSET_BEGINNING:
                    newoffset = offset;
                    break;
		
                case Constants.OFFSET_END:
                    // #if DELDIR
			        if (delfile != null)
				        newoffset = (int)(Directory.GetDDFileSize(delfile, g) + offset);
			        else
                    // #endif
                        newoffset = (int)(Directory.GetDEFileSize(file.le.info.file.direntry, g) + offset);
                    break;
		
                case Constants.OFFSET_CURRENT:
                    newoffset = oldoffset + offset;
                    break;
		
                default:
                    //*error = ERROR_SEEK_ERROR;
                    return -1;
            }

// #if DELDIR
	if ((newoffset > (delfile != null ? Directory.GetDDFileSize(delfile, g) :
		Directory.GetDEFileSize(file.le.info.file.direntry, g))) || (newoffset < 0))
// #else
//             if ((newoffset > GetDEFileSize(file->le.info.file.direntry)) || (newoffset < 0))
// #endif
            {
                //*error = ERROR_SEEK_ERROR;
                return -1;
            }

            /* calculate new values */
            anodeoffset = (uint)(newoffset >> g.blockshift);
            blockoffset = (uint)(newoffset & Macro.BLOCKSIZEMASK(g));
            file.currnode = file.anodechain.head;
            anodes.CorrectAnodeAC(file.currnode, anodeoffset, g);
            /* DiskSeek(anode.blocknr + anodeoffset, g); */
	
            file.anodeoffset  = anodeoffset;
            file.blockoffset  = blockoffset;
            file.offset       = (uint)newoffset;
            return oldoffset;
        }
        
/* flush all blocks in datacache (without updating them first).
 */
        public static void FlushDataCache(globaldata g)
        {
            for (var i = 0; i < g.dc.size; i++)
            {
                g.dc.ref_[i].blocknr = 0;
            }
        }
    }
}