namespace Hst.Amiga.FileSystems.Pfs3
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
            if (!(Macro.InPartition(blocknr, g) && Macro.InPartition(blocknr + blocks - 1, g)))
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
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: Raw read bytes from block nr {blocknr} with size of {blocks} blocks");
#endif
            
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
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: Raw read block type '{typeof(T).Name}' from block nr {blocknr} with size of {blocks} blocks");
#endif

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
            return await RawWrite(stream, buffer, 0, blocks, blocknr, g);
        }

        public static async Task<bool> RawWrite(Stream stream, byte[] buffer, int offset, uint blocks, uint blocknr,
            globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: Raw write bytes to block nr {blocknr} with size of {blocks} blocks");
#endif
            // RawReadWrite_DS(TRUE, buffer, blocks, blocknr, g);

            if (blocknr == UInt32.MaxValue) // blocknr of uninitialised anode
                return false;

            blocknr += g.firstblock;

            if (g.softprotect)
            {
                throw new IOException("ERROR_DISK_WRITE_PROTECTED");
            }

            BoundsCheck(true, blocknr, blocks, g);

            var blockOffset = (long)g.blocksize * blocknr;
            g.stream.Seek(blockOffset, SeekOrigin.Begin);

            var writeLength = (int)(g.blocksize * blocks);

            if (buffer.Length == writeLength)
            {
                await stream.WriteBytes(buffer);
            }
            else
            {
                await stream.WriteAsync(buffer, offset, Math.Min(writeLength, buffer.Length));
                
            }

            
            //
            // // zero fill, if write length is larger than buffer
            // if (offset + writeLength > buffer.Length)
            // {
            //     var zeroFill = new byte[offset + writeLength - buffer.Length];
            //     await stream.WriteAsync(zeroFill, 0, zeroFill.Length);
            // }
            
            return true;
        }

        public static async Task<bool> RawWrite(Stream stream, IBlock block, uint blocks, uint blocknr, globaldata g)
        {
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: Raw write block type '{block.GetType().Name}' to block nr {blocknr} with size of {blocks} blocks");
#endif

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
        public static async Task UpdateSlot(int slotnr, globaldata g)
        {
            uint blocknr;
            int i;

            blocknr = g.dc.ref_[slotnr].blocknr;

            /* find out how many adjacent blocks can be written */
            for (i = slotnr; i < g.dc.size; i++)
            {
                if (g.dc.ref_[i].blocknr != blocknr++)
                    break;
                g.dc.ref_[i].dirty = false;
            }

            /* write them */
            //await RawWrite(g.dc.data[slotnr << g.blockshift], i-slotnr, g.dc.ref_[slotnr].blocknr, g);
            await RawWrite(g.stream, g.dc.data, slotnr << g.blockshift, (uint)(i - slotnr), g.dc.ref_[slotnr].blocknr,
                g);
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
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: SeekInFile, offset = {offset}, mode = {mode}");
#endif
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
            if ((newoffset > (delfile != null
                    ? Directory.GetDDFileSize(delfile, g)
                    : Directory.GetDEFileSize(file.le.info.file.direntry, g))) || (newoffset < 0))
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
            anodes.CorrectAnodeAC(file.currnode, ref anodeoffset, g);
            /* DiskSeek(anode.blocknr + anodeoffset, g); */

            file.anodeoffset = anodeoffset;
            file.blockoffset = blockoffset;
            file.offset = (uint)newoffset;
            return newoffset;
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

/* <ReadFromFile>
**
** Specification:
**
** Reads 'size' bytes from file to buffer (if not readprotected)
** result: #bytes read; -1 = error; 0 = eof
*/
        public static async Task<uint> ReadFromFile(fileentry file, byte[] buffer, uint size, globaldata g)
        {
            var BLOCKSIZE = Macro.BLOCKSIZE(g);
            var BLOCKSHIFT = Macro.BLOCKSHIFT(g);
            var BLOCKSIZEMASK = Macro.BLOCKSIZEMASK(g);
            var DIRECTSIZE = Macro.DIRECTSIZE(g);

            uint anodeoffset, blockoffset, blockstoread;
            uint fullblks, bytesleft;
            uint t;
            uint tfs;
            //UBYTE *data = NULL, *dataptr;
            byte[] data = new byte[0];
            int dataptr = 0;
            bool directread = false;
            anodechainnode chnode;
            // #if DELDIR
            deldirentry dde;
            // #endif
            
            //DB(Trace(1,"ReadFromFile","size = %lx offset = %lx\n",size,file->offset));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: ReadFromFile, size = {size}, offset = {file.offset}");
#endif
            CheckAccess.CheckReadAccess(file, g);

            /* correct size and check if zero */
// #if DELDIR
            if (Macro.IsDelFile(file.le.info))
            {
                if ((dde = await Directory.GetDeldirEntryQuick(file.le.info.delfile.slotnr, g)) == null)
                {
                    return UInt32.MaxValue;
                }

                tfs = Directory.GetDDFileSize(dde, g) - file.offset;
            }
            else
// #endif
            {
                tfs = Directory.GetDEFileSize(file.le.info.file.direntry, g) - file.offset;
            }

            if ((size = Math.Min(tfs, size)) == 0)
            {
                return 0;
            }

            /* initialize */
            anodeoffset = file.anodeoffset;
            blockoffset = file.blockoffset;
            chnode = file.currnode;
            t = blockoffset + size;
            fullblks = t >> BLOCKSHIFT; /* # full blocks */
            bytesleft = t & BLOCKSIZEMASK; /* # bytes in last incomplete block */

            /* check mask, both at start and end */
            t = ((buffer.Length - blockoffset + BLOCKSIZE) & ~g.DosEnvec.de_Mask) != 0 ||
                ((buffer.Length + size - bytesleft) & ~g.DosEnvec.de_Mask) != 0
                ? 1U
                : 0U;
            t = t != 0U ? 0U : 1U;

            /* read indirect if
             * - mask failure
             * - too small
             * - larger than one block (use 'direct' cached read for just one)
             */
            if (t == 0 || (fullblks < 2 * DIRECTSIZE && (blockoffset + size > BLOCKSIZE) &&
                           (blockoffset != 0 || (bytesleft != 0 && fullblks < DIRECTSIZE))))
            {
                /* full indirect read */
                blockstoread = (uint)(fullblks + (bytesleft > 0 ? 1 : 0));
                // if (!(data = AllocBufmem (blockstoread<<BLOCKSHIFT, g)))
                // {
                // 	throw new IOException("ERROR_NO_FREE_STORE");
                // }
                data = new byte[blockstoread << BLOCKSHIFT];
                dataptr = 0;
            }
            else
            {
                /* direct read */
                directread = true;
                blockstoread = fullblks;
                //dataptr = buffer;
                data = buffer;
                dataptr = 0;

                /* read first blockpart */
                if (blockoffset != 0)
                {
                    var bytesRead = await CachedReadD(chnode.an.blocknr + anodeoffset, g);
                    if (bytesRead.Length > 0)
                    {
                        anodes.NextBlockAC(chnode, ref anodeoffset, g);

                        /* calc numbytes */
                        t = BLOCKSIZE - blockoffset;
                        t = Math.Min(t, size);
                        //memcpy(dataptr, data+blockoffset, t);
                        Array.Copy(bytesRead, blockoffset, data, dataptr, t);
                        dataptr += (int)t;
                        if (blockstoread != 0)
                            blockstoread--;
                        else
                            bytesleft = 0; /* single block access */
                    }
                }
            }

            /* read middle part */
            while (blockstoread != 0)
            {
                if ((blockstoread + anodeoffset) >= chnode.an.clustersize)
                    t = chnode.an.clustersize - anodeoffset; /* read length */
                else
                    t = blockstoread;

                // *error = DiskRead(dataptr, t, chnode->an.blocknr + anodeoffset, g);
                var bytesRead = await RawRead(t, chnode.an.blocknr + anodeoffset, g);
                Array.Copy(bytesRead, 0, data, dataptr, Math.Min(bytesRead.Length, data.Length));
                // if (!*error)
                // {
                blockstoread -= t;
                dataptr += (int)(t << BLOCKSHIFT);
                anodeoffset += t;
                anodes.CorrectAnodeAC(chnode, ref anodeoffset, g);
                // }
            }

            /* read last block part/ copy read data to buffer */
            // if (!*error)
            // {
            if (!directread)
            {
                //memcpy(buffer, data+blockoffset, size);
                Array.Copy(data, blockoffset, buffer, 0, size);
            }
            else if (bytesleft > 0)
            {
                var dataRead = await CachedReadD(chnode.an.blocknr + anodeoffset, g);
                if (dataRead.Length > 0)
                {
                    //memcpy(dataptr, data, bytesleft);
                    Array.Copy(dataRead, 0, buffer, dataptr, bytesleft);
                }
            }
            // }

            // if (!directread)
            // 	FreeBufmem(data, g);
            // if (!*error)
            // {
            file.anodeoffset += fullblks;
            file.blockoffset = (file.blockoffset + size) & BLOCKSIZEMASK; // not bytesleft!!
            anodes.CorrectAnodeAC(file.currnode, ref file.anodeoffset, g);
            file.offset += size;
            return size;
            // }
            // else
            // {
            //DB(Trace(1,"Read","failed\n"));
            // return UInt32.MaxValue;
            // }
        }

/* <WriteToFile> 
**
** Specification:
**
** - Copy data in file at current position;
** - Automatic fileextension;
** - Error = bytecount <> opdracht
** - On error no position update
**
** - Clear Archivebit -> done by Touch()
**V- directory protection (amigados does not do this)
**
** result: num bytes written; DOPUS wants -1 = error;
**
** Implementation parts
**
** - Test on writeprotection; yes -> error;
** - Initialisation
** - Extend filesize
** - Write firstblockpart
** - Write all whole blocks
** - Write last block
** - | Update directory (if no errors)
**   | Deextent filesize (if error)
*/
        public static async Task<uint> WriteToFile(fileentry file, byte[] buffer, uint size, globaldata g)
        {
            var BLOCKSIZE = Macro.BLOCKSIZE(g);
            var BLOCKSHIFT = Macro.BLOCKSHIFT(g);
            var BLOCKSIZEMASK = Macro.BLOCKSIZEMASK(g);
            var DIRECTSIZE = Macro.DIRECTSIZE(g);

            bool maskok;
            uint t;
            uint totalblocks, oldblocksinfile;
            uint oldfilesize;
            uint newfileoffset;
            uint newblocksinfile;
            uint bytestowrite, blockstofill;
            uint anodeoffset, blockoffset;
            //UBYTE* data = NULL,  *dataptr;
            //var data = new List<byte>();
            byte[] data;
            int dataptr = 0;
            // bool directwrite = false;
            anodechainnode chnode;
            int slotnr;

            //DB(Trace(1,"WriteToFile","size = %lx offset=%lx, file=%lx\n",size,file->offset,file));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: WriteToFile, size = {size}, offset = {file.offset}");
#endif
            /* initialization values */
            chnode = file.currnode;
            anodeoffset = file.anodeoffset;
            blockoffset = file.blockoffset;
            totalblocks =
                (blockoffset + size + BLOCKSIZEMASK) >> BLOCKSHIFT; /* total # changed blocks */
            if ((bytestowrite = size) == 0) /* # bytes to be done */
                return 0;

            /* filesize extend */
            oldfilesize = Directory.GetDEFileSize(file.le.info.file.direntry, g);
            newfileoffset = file.offset + size;

            /* Check if too large (QUAD) or overflowed (ULONG)? */
            if (newfileoffset > Constants.MAX_FILE_SIZE || newfileoffset < file.offset)
            {
                throw new IOException("ERROR_DISK_FULL");
            }

            oldblocksinfile = (oldfilesize + BLOCKSIZEMASK) >> BLOCKSHIFT;
            newblocksinfile = (newfileoffset + BLOCKSIZEMASK) >> BLOCKSHIFT;
            if (newblocksinfile > oldblocksinfile)
            {
                t = newblocksinfile - oldblocksinfile;
                if (!await Allocation.AllocateBlocksAC(file.anodechain, t, file.le.info.file, g))
                {
                    file.le.info.file.direntry = Directory.SetDEFileSize(file.le.info.file.dirblock.dirblock, file.le.info.file.direntry, oldfilesize, g);
                    throw new IOException("ERROR_DISK_FULL");
                }
            }

            /* BUG 980422: this CorrectAnodeAC mode because of AllocateBlockAC!! AND
             * because anodeoffset can be outside last block! (filepointer is
             * byte 0 new block
             */
            anodes.CorrectAnodeAC(chnode, ref anodeoffset, g);

            /* check mask */
            maskok = ((buffer.Length - blockoffset + BLOCKSIZE) & ~g.DosEnvec.de_Mask) != 0 ||
                     ((buffer.Length - blockoffset + (totalblocks << BLOCKSHIFT)) & ~g.DosEnvec.de_Mask) != 0;
            maskok = !maskok;

            /* write indirect if
             * - mask failure
             * - too small
             */
            if (!maskok || (totalblocks < 2 * DIRECTSIZE && (blockoffset + size > BLOCKSIZE * 2) &&
                            (blockoffset != 0 || totalblocks < DIRECTSIZE)))
            {
                /* indirect */
                /* allocate temporary data buffer */
                // if (!(dataptr = data = AllocBufmem(totalblocks << BLOCKSHIFT, g)))
                // {
                //     throw new IOException("ERROR_NO_FREE_STORE");
                //     goto wtf_error;
                // }
                data = new byte[totalblocks << BLOCKSHIFT];

                /* first blockpart */
                if (blockoffset != 0)
                {
                    //*error = DiskRead(dataptr, 1, chnode.an.blocknr + anodeoffset, g);
                    var dataRead = await RawRead(1, chnode.an.blocknr + anodeoffset, g);
                    Array.Copy(dataRead, 0, buffer, dataptr, dataRead.Length);
                    bytestowrite += blockoffset;
                    if (bytestowrite < BLOCKSIZE)
                        bytestowrite = BLOCKSIZE; /* the first could also be the last block */
                }

                /* copy all 'to be written' to databuffer */
                //memcpy(dataptr + blockoffset, buffer, size);
                Array.Copy(buffer, 0, data, dataptr, size);
            }
            else
            {
                /* direct */
                //dataptr = buffer;
                data = buffer;
                dataptr = 0;
                //directwrite = true;

                /* first blockpart */
                if (blockoffset != 0 || (totalblocks == 1 && newfileoffset > oldfilesize))
                {
                    uint fbp; /* first block part */
                    //UBYTE* firstblock;

                    if (blockoffset != 0)
                    {
                        slotnr = await CachedRead(chnode.an.blocknr + anodeoffset, false, g);
                        // if (*error)
                        //     goto wtf_error;
                    }
                    else
                    {
                        /* for one block no offset growing file */
                        slotnr = await CachedRead(chnode.an.blocknr + anodeoffset, true, g);
                    }

                    /* copy data to cache and mark block as dirty */
                    //firstblock = &g->dc.data[slotnr << BLOCKSHIFT];
                    var firstblock = slotnr << BLOCKSHIFT;
                    fbp = BLOCKSIZE - blockoffset;
                    fbp = Math.Min(bytestowrite, fbp); /* the first could also be the last block */
                    //memcpy(firstblock + blockoffset, buffer, fbp);
                    Array.Copy(buffer, dataptr, g.dc.data, firstblock, fbp);
                    Macro.MarkDataDirty(slotnr, g);

                    anodes.NextBlockAC(chnode, ref anodeoffset, g);
                    bytestowrite -= fbp;
                    dataptr += (int)fbp;
                    totalblocks--;
                }
            }

            /* write following blocks. If done, then blockoffset always 0 */
            if (newfileoffset > oldfilesize)
            {
                blockstofill = totalblocks;
            }
            else
            {
                blockstofill = bytestowrite >> BLOCKSHIFT;
            }

            while (blockstofill != 0)
            {
                // UBYTE* lastpart = NULL;
                // UBYTE* writeptr;
                // UBYTE* lastpart = NULL;
                // UBYTE* writeptr;
                byte[] lastpart = null;

                if (blockstofill + anodeoffset >= chnode.an.clustersize)
                    t = chnode.an.clustersize - anodeoffset; /* t is # blocks to write now */
                else
                    t = blockstofill;

                byte[] writeptr = data;
                // last write, writing to end of file and last block won't be completely filled?
                // all this just to prevent out of bounds memory read access.
                if (t == blockstofill && (bytestowrite & BLOCKSIZEMASK) != 0 && newfileoffset > oldfilesize)
                {
                    // limit indirect to max 2 * DIRECTSIZE
                    if (t > 2 * DIRECTSIZE)
                    {
                        // > 2 * DIRECTSIZE: write only last partial block indirectly
                        t--;
                    }
                    else
                    {
                        lastpart = new byte[(int)t << BLOCKSHIFT];
                        // indirect write last block(s), including final partial block.
                        // if (!(lastpart = AllocBufmem(t << BLOCKSHIFT, g)))
                        // {
                        //     if (t == 1)
                        //     {
                        //         // no memory, do slower cached final partial block write
                        //         goto indirectlastwrite;
                        //     }
                        //
                        //     t /= 2;
                        // }
                        // else
                        // {
                        //memcpy(lastpart, dataptr, bytestowrite);
                        Array.Copy(data, dataptr, lastpart, 0, bytestowrite);
                        writeptr = lastpart;
                        // }
                    }
                }

                // *error = DiskWrite(writeptr, t, chnode->an.blocknr + anodeoffset, g);
                if (await RawWrite(g.stream, writeptr, t, chnode.an.blocknr + anodeoffset, g))
                {
                    blockstofill -= t;
                    dataptr += (int)(t << BLOCKSHIFT);
                    bytestowrite -= t << BLOCKSHIFT;
                    anodeoffset += t;
                    anodes.CorrectAnodeAC(chnode, ref anodeoffset, g);
                }

                if (lastpart != null)
                {
                    bytestowrite = 0;
                    //FreeBufmem(lastpart, g);
                    lastpart = null;
                }
            }

            //indirectlastwrite:
            /* write last block (RAW because cache direct), preserve block's old contents */
            if (bytestowrite != 0)
            {
                //UBYTE* lastblock;

                slotnr = await CachedRead(chnode.an.blocknr + anodeoffset, false, g);
                // if (!*error)
                // {
                //lastblock = g.dc.data[slotnr << BLOCKSHIFT];
                var lastBlock = slotnr << BLOCKSHIFT;
                //memcpy(lastblock, dataptr, bytestowrite);
                Array.Copy(g.dc.data, lastBlock, buffer, dataptr, bytestowrite);
                Macro.MarkDataDirty(slotnr, g);
                // }
            }

            /* free mem for indirect write */
            // if (!directwrite)
            //     FreeBufmem(data, g);
            // if (!*error)
            // {
            file.anodeoffset += (blockoffset + size) >> BLOCKSHIFT;
            file.blockoffset = (blockoffset + size) & BLOCKSIZEMASK;
            anodes.CorrectAnodeAC(file.currnode, ref file.anodeoffset, g);
            file.offset += size;
            file.le.info.file.direntry = Directory.SetDEFileSize(file.le.info.file.dirblock.dirblock, file.le.info.file.direntry, Math.Max(oldfilesize, file.offset), g);
            await Update.MakeBlockDirty(file.le.info.file.dirblock, g);
            return size;
            // }

            // UNUSED: Commented out as exception is thrown instead
//             wtf_error:
//             if (newblocksinfile > oldblocksinfile)
//             {
//                 /* restore old state of file */
// // #if VERSION23
//                 Directory.SetDEFileSize(file.le.info.file.dirblock.dirblock, file.le.info.file.direntry, oldfilesize, g);
//                 await Update.MakeBlockDirty(file.le.info.file.dirblock, g);
//                 await Allocation.FreeBlocksAC(file.anodechain, newblocksinfile - oldblocksinfile,
//                     freeblocktype.freeanodes, g);
// // #else
// // 		FreeBlocksAC(file->anodechain, newblocksinfile-oldblocksinfile, freeanodes, g);
// // 		SetDEFileSize(file->le.info.file.direntry, oldfilesize, g);
// // 		await Update.MakeBlockDirty((struct cachedblock *)file->le.info.file.dirblock, g);
// // #endif
//             }
//
//             //DB(Trace(1,"WriteToFile","failed\n"));
//             return UInt32.MaxValue;
        }

/* check datacache. return cache slotnr or -1
 * if not found
 */
        public static int CheckDataCache(uint blocknr, globaldata g)
        {
            int i;

            for (i = 0; i < g.dc.size; i++)
            {
                if (g.dc.ref_[i].blocknr == blocknr)
                {
                    return i;
                }
            }

            return -1;
        }

/* get block from cache or put it in cache if it wasn't
 * there already. return cache slotnr. errors are indicated by 'error'
 * (null = ok)
 */
        public static async Task<int> CachedRead(uint blocknr, bool fake, globaldata g)
        {
            int i;

            i = CheckDataCache(blocknr, g);
            if (i != -1) return i;
            i = g.dc.roving;
            if (g.dc.ref_[i].dirty && g.dc.ref_[i].blocknr != 0)
            {
                await UpdateSlot(i, g);
            }

            if (fake)
            {
                // memset(&g->dc.data[i<<BLOCKSHIFT], 0xAA, BLOCKSIZE);
                for (var f = i << (int)Macro.BLOCKSHIFT(g); f < Macro.BLOCKSIZE(g); f++)
                {
                    g.dc.data[f] = 0xAA;
                }
            }
            else
            {
                // *error = RawRead(&g->dc.data[i<<BLOCKSHIFT], 1, blocknr, g);
                var data = await RawRead(1, blocknr, g); 
                Array.Copy(data, 0, g.dc.data, i << Macro.BLOCKSHIFT(g), data.Length);
            }

            g.dc.roving = (ushort)((g.dc.roving + 1) & g.dc.mask);
            g.dc.ref_[i].dirty = false;
            g.dc.ref_[i].blocknr = blocknr;
            return i;
        }

        public static async Task<byte[]> CachedReadD(uint blknr, globaldata g)
        {
            var i = await CachedRead(blknr, false, g);
            // if (*err)   
            //     return NULL;
            // else
            // return &g->dc.data[i<<BLOCKSHIFT];
            var blockSize = Macro.BLOCKSIZE(g);
            var buffer = new byte[blockSize];
            Array.Copy(g.dc.data, i<<Macro.BLOCKSHIFT(g), buffer, 0, blockSize);
            return buffer;
        }

/* Read from rollover: at end of file,
 * goto start
 */
        public static async Task<uint> ReadFromRollover(fileentry file, byte[] buffer, uint size, globaldata g)
        {
// #define direntry_m file->le.info.file.direntry
// #define filesize_m GetDEFileSize(file->le.info.file.direntry, g)
            var direntry_m = file.le.info.file.direntry;
            var filesize_m = Directory.GetDEFileSize(file.le.info.file.direntry, g);

            extrafields extrafields = new extrafields();
            uint read = 0;
            int q; // quantity
            int end, virtualoffset, virtualend, t;

            //DB(Trace(1,"ReadFromRollover","size = %lx offset = %lx\n",size,file->offset));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: ReadFromRollover, size = {size}, offset = {file.offset}");
#endif
            if (size == 0)
            {
                return 0;
            }

            Directory.GetExtraFields(direntry_m, extrafields);

            /* limit access to end of file */
            virtualoffset = (int)(file.offset - extrafields.rollpointer);
            if (virtualoffset < 0)
            {
                virtualoffset += (int)filesize_m;
            }

            virtualend = (int)(virtualoffset + size);
            virtualend = (int)Math.Min(virtualend, extrafields.virtualsize);
            end = (int)(virtualend - virtualoffset + file.offset);

            int bufferPos = 0;
            byte[] bufferRead;

            if (end > filesize_m)
            {
                q = (int)(filesize_m - file.offset);
                bufferRead = new byte[q];
                if ((read = await ReadFromFile(file, bufferRead, (uint)q, g)) != q)
                {
                    return read;
                }

                end -= (int)filesize_m;
                //buffer += (uint)q;
                Array.Copy(bufferRead, 0, buffer, bufferPos, q);
                bufferPos += q;
                await SeekInFile(file, 0, Constants.OFFSET_BEGINNING, g);
            }

            q = (int)(end - file.offset);
            bufferRead = new byte[q];
            t = (int)await ReadFromFile(file, bufferRead, (uint)q, g);
            Array.Copy(bufferRead, 0, buffer, bufferPos, q);
            if (t == -1)
                return (uint)t;
            else
                read += (uint)t;

            return read;

            // #undef filesize_m
            // #undef direntry_m
        }

/* Write to rollover file. First write upto end of rollover. Then
 * flip to start.
 * Max virtualsize = filesize-1
 */
        public static async Task<uint> WriteToRollover(fileentry file, byte[] buffer, uint size, globaldata g)
        {
            //#define direntry_m file->le.info.file.direntry
            //#define filesize_m GetDEFileSize(file->le.info.file.direntry, g)
            var direntry_m = file.le.info.file.direntry;
            var filesize_m = Directory.GetDEFileSize(file.le.info.file.direntry, g);

            extrafields extrafields = new extrafields();
            direntry destentry;
            objectinfo directory = new objectinfo();
            fileinfo fi = new fileinfo();
            var entrybuffer = new byte[Macro.MAX_ENTRYSIZE];
            int written = 0;
            int q; // quantity
            int end, virtualend, virtualoffset, t;
            bool extend = false;

            //DB(Trace(1,"WriteToRollover","size = %lx offset=%lx, file=%lx\n",size,file->offset,file));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: WriteToRollover, size = {size}, offset = {file.offset}");
#endif
            Directory.GetExtraFields(direntry_m, extrafields);
            end = (int)(file.offset + size);

            /* new virtual size */
            virtualoffset = (int)(file.offset - extrafields.rollpointer);
            if (virtualoffset < 0)
            {
                virtualoffset += (int)filesize_m;
            }

            virtualend = (int)(virtualoffset + size);
            if (virtualend >= extrafields.virtualsize)
            {
                extrafields.virtualsize = (uint)Math.Min(filesize_m - 1, virtualend);
                extend = true;
            }

            byte[] writeBuffer;
            int bufferPos = 0;
            while (end > filesize_m)
            {
                q = (int)(filesize_m - file.offset);
                writeBuffer = new byte[q];
                Array.Copy(buffer, bufferPos, writeBuffer, 0, q);
                t = (int)await WriteToFile(file, writeBuffer, (uint)q, g);
                if (t == -1) return (uint)t;
                written += t;
                if (t != q) return (uint)written;
                end -= (int)filesize_m;
                bufferPos += q;
                await SeekInFile(file, 0, Constants.OFFSET_BEGINNING, g);
            }

            q = (int)(end - file.offset);
            writeBuffer = new byte[q];
            Array.Copy(buffer, bufferPos, writeBuffer, 0, q);
            t = (int)await WriteToFile(file, buffer, (uint)q, g);
            if (t == -1)
                return (uint)t;
            else
                written += t;

            /* change rollpointer etc */
            if (extend && extrafields.virtualsize == filesize_m - 1)
                extrafields.rollpointer = (uint)(end + 1); /* byte PAST eof is offset 0 */
            //destentry = (struct direntry *)entrybuffer;
            destentry = direntry_m;
            //memcpy(destentry, direntry_m, direntry_m->next);
            Directory.AddExtraFields(destentry, extrafields);

            /* commit changes */
            if (!await Directory.GetParent(file.le.info, directory, g))
                return 0;
            else
                await Directory.ChangeDirEntry(file.le.info, destentry, directory, fi, g);

            return (uint)written;

// #undef direntry_m
// #undef filesize_m
        }
        
        public static async Task<int> SeekInObject(fileentry file, int offset, int mode, globaldata g)
        {
            /* check access */
            CheckAccess.CheckOperateFile(file, g);

            /* check anodechain, make if not there */
            if (file.anodechain == null)
            {
                if ((file.anodechain = await anodes.GetAnodeChain(file.le.anodenr, g)) == null)
                {
                    throw new IOException("ERROR_NO_FREE_STORE");
                }
            }

            // #if ROLLOVER
	        if (Macro.IsRollover(file.le.info))
		        return SeekInRollover(file,offset,mode,g);
	        else
                // #endif
            return await SeekInFile(file,offset,mode,g);
        }
        
        static int SeekInRollover(fileentry file, int offset, int mode, globaldata g)
        {
            var BLOCKSHIFT = Macro.BLOCKSHIFT(g);
            var BLOCKSIZEMASK = Macro.BLOCKSIZEMASK(g);

            // #define filesize_m GetDEFileSize(file->le.info.file.direntry, g)
            // #define direntry_m file->le.info.file.direntry
            var filesize_m = Directory.GetDEFileSize(file.le.info.file.direntry, g);
            var direntry_m = file.le.info.file.direntry;

            extrafields extrafields = new extrafields();
            int oldvirtualoffset, virtualoffset;
            uint anodeoffset, blockoffset;

            //DB(Trace(1,"SeekInRollover","offset = %ld mode=%ld\n",offset,mode));
#if DEBUG
            Pfs3Logger.Instance.Debug($"Disk: SeekInRollover, offset = {offset}, mode = {mode}");
#endif
            Directory.GetExtraFields(direntry_m, extrafields);

            /* do the seeking */
            oldvirtualoffset = (int)(file.offset - extrafields.rollpointer);
            if (oldvirtualoffset < 0)
            {
                oldvirtualoffset += (int)filesize_m;
            }

            switch (mode)
            {
                case Constants.OFFSET_BEGINNING:
                    virtualoffset = offset;
                    break;

                case Constants.OFFSET_END:
                    virtualoffset = (int)(extrafields.virtualsize + offset);
                    break;
		
                case Constants.OFFSET_CURRENT:
                    virtualoffset = oldvirtualoffset + offset;
                    break;
		
                default:
                    throw new IOException("ERROR_SEEK_ERROR");
            }

            if (virtualoffset > extrafields.virtualsize || virtualoffset < 0)
            {
                throw new IOException("ERROR_SEEK_ERROR");
            }

            /* calculate real offset */
            file.offset = (uint)(virtualoffset + extrafields.rollpointer);
            if (file.offset > filesize_m)
            {
                file.offset -= filesize_m;
            }

            /* calculate new values */
            anodeoffset = file.offset >> BLOCKSHIFT;
            blockoffset = file.offset & BLOCKSIZEMASK;
            file.currnode = file.anodechain.head;
            anodes.CorrectAnodeAC(file.currnode, ref anodeoffset, g);
	
            file.anodeoffset  = anodeoffset;
            file.blockoffset  = blockoffset;

            return oldvirtualoffset;

// #undef filesize_m
// #undef direntry_m
        }
    }
}