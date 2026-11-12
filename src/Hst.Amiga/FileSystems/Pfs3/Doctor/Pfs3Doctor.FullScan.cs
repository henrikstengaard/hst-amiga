namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Port of fullscan.c.
    /// </summary>
    public partial class Pfs3Doctor
    {
        private uint maxdatestamp;

        private void AddBuildBlockNode(string name, CachedBlock bbl)
        {
            volume.BuildBlocks.AddFirst(bbl);
        }

        private void InitFullScan()
        {
            volume.BuildBlocks = new System.Collections.Generic.LinkedList<CachedBlock>();
        }

        /* free mem etc */
        private void ExitFullScan()
        {
            volume.BuildBlocks.Clear();
        }

        /* Allocate create blocks. Write them to the cache and free
         * memory.
         * pre: resbitmap ready and valid
         */
        private async Task<Error> AllocBuildBlocks()
        {
            var error = Error.None;
            uint blocknr, offset, seqnr;
            ushort type = 0;

            for (var node = volume.BuildBlocks.First; node != null;)
            {
                var bbl = node.Value;
                volume.Progress(0, 1);
                var next = node.Next;
                blocknr = FsAllocResBlock();
                if (blocknr != 0)
                {
                    bbl.BlockNr = blocknr;
                    bbl.Mode = Mode.Done;
                    await VolWriteBlock(bbl);
                    switch (bbl.Id)
                    {
                        case Constants.EXTENSIONID:
                            rbl.Extension = blocknr;
                            await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                volume.BlockSize);
                            rext.Mode = Mode.Check;
                            rext.Data = new byte[SizeOfResBlock];
                            rext.BlockNr = blocknr;
                            Array.Copy(bbl.Data, rext.Data, SizeOfResBlock);
                            break;

                        case Constants.SBLKID:
                            rext.ExtensionBlock.SuperIndex(bbl.IndexBlock.SeqNr).Value = blocknr;
                            await VolWriteBlock(rext);
                            break;

                        case Constants.BMIBLKID:
                            rbl.IdxLargeBitmapIndex(bbl.IndexBlock.SeqNr).Value = blocknr;
                            await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                volume.BlockSize);
                            break;

                        case Constants.IBLKID:
                            /* MODE_SUPERINDEX, not MODE_SUPERDELDIR, decides
                             * whether anode index blocks hang off a superblock
                             * (cf. GetResBlock). Harmless while BuildRootBlock
                             * set neither bit; with supermode rebuilt this would
                             * store a superblock index in idx.small.indexblocks,
                             * which overlaps idx.large.bitmapindex.
                             */
                            if ((rbl.Options & Constants.MODE_SUPERINDEX) == 0)
                            {
                                rbl.IdxSmallIndexBlocks(bbl.IndexBlock.SeqNr).Value = blocknr;
                                await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                    volume.BlockSize);
                                break;
                            }

                            type = Constants.SBLKID;
                            goto case Constants.BMBLKID;

                        case Constants.BMBLKID:
                            if (bbl.Id == Constants.BMBLKID)
                                type = Constants.BMIBLKID;

                            seqnr = bbl.IndexBlock.SeqNr / IndexPerBlock;
                            offset = bbl.IndexBlock.SeqNr % IndexPerBlock;
                            var tblk = new CachedBlock { Data = new byte[SizeOfResBlock] };
                            await GetResBlock(tblk, type, seqnr, true);
                            tblk.IndexBlock.Index(offset).Value = blocknr;
                            await VolWriteBlock(tblk);
                            break;
                    }

                    volume.BuildBlocks.Remove(node);
                }
                else
                {
                    AddError("Allocation error");
                    error = Error.AllocFail;
                }

                node = next;
            }

            return error;
        }

        /* Allocate a block from the generated reserved bitmap.
         * This reserved bitmap must be complete and valid
         */
        private uint FsAllocResBlock()
        {
            uint i, field, blocknr;
            int j;

            for (i = 0; i < volume.ResBitmap.LwSize; i++)
            {
                field = volume.ResBitmap.Map[i];
                if (field != 0)
                {
                    for (j = 31; j >= 0; j--)
                    {
                        if ((field & (1U << j)) != 0)
                        {
                            blocknr = rbl.FirstReserved + (i * 32 + (31 - (uint)j)) * volume.ResCluster;
                            if (blocknr < rbl.LastReserved)
                            {
                                volume.ResBitmap.Map[i] &= ~(1U << j);
                                return blocknr;
                            }
                        }
                    }
                }
            }

            /* reserved area full */
            return 0;
        }

        /* Redefine volume from rootblock located at 'bloknr'
         */
        private async Task<Error> Repartition(uint bloknr)
        {
            Error error;

            if (bloknr < DoctorConstants.ROOTBLOCK)
                return Error.SyntaxError;

            /* not the global rbl */
            var nrbl = new RootBlock(new byte[DoctorConstants.MAXRESBLOCKSIZE]);

            /* Read the rootblock this call is about. Reading the standard
             * location instead made the misplaced-rootblock path re-read the
             * block whose absence started the search, so it always failed the
             * check below - the one caller that passes a searched-for block
             * number was the one that could not work.
             */
            error = await CGetBlock(nrbl.Data, bloknr, volume.BlockSize);
            if (error != Error.None)
                return error;

            if (!IsRootBlock(nrbl) || (nrbl.DiskSize >> volume.BlockLogShift) == 0)
            {
                return Error.SyntaxError;
            }

            /* the size field is in native sectors, the block numbers are in
             * logical blocks; treating one as the other redefined the volume
             * 2, 4 or 8 times too large on a >512 byte block volume, putting
             * lastblock - and with it every bounds check - past the end of the
             * partition
             */
            volume.FirstBlock = bloknr - DoctorConstants.ROOTBLOCK;
            volume.DiskSize = nrbl.DiskSize >> volume.BlockLogShift;
            volume.LastBlock = volume.FirstBlock + volume.DiskSize - 1;

            /* the volume is now described by this rootblock, so the native
             * values have to follow it rather than keep describing the RDB
             * partition that was just rejected
             */
            volume.FirstBlockNative = volume.FirstBlock << volume.BlockLogShift;
            volume.LastBlockNative = ((volume.LastBlock + 1) << volume.BlockLogShift) - 1;
            volume.DiskSizeNative = (volume.LastBlock - volume.FirstBlock) << volume.BlockLogShift;
            volume.FullDiskSizeNative = nrbl.DiskSize;

            volume.LastReserved = nrbl.LastReserved;
            volume.Repartitioned = true;

            return Error.None;
        }

        /* special case: no alloc needed */
        private async Task<Error> BuildBootBlock()
        {
            var bbl = new byte[2 * volume.BlockSize];
            new BootBlock(bbl).DiskType = Constants.ID_PFS_DISK;
            // original code has bloknr and bytes arguments swapped
            return await CWriteBlock(bbl, DoctorConstants.BOOTBLOCK + volume.FirstBlock, 2 * volume.BlockSize);
        }

        /* special case: no alloc needed
         * Create rootblock. The datestamp is set to the highest value found by
         * SeachBlocks, increased by 0x299. All references are uptodate.
         */
        private async Task<Error> BuildRootBlock(RootBlock rbl)
        {
            uint i, j;
            var ind = NewScanElements(Constants.MAXBITMAPINDEX + 1);
            uint resblocksize;

            volume.Status(0, "Building rootblock", 3);
            Array.Clear(rbl.Data, 0, (int)volume.BlockSize);
            var time = Amiga.DateHelper.ConvertToAmigaDate(DateTime.Now);

            rbl.DiskType = Constants.ID_PFS_DISK;
            /* MODE_EXTENSION because BuildRext() always builds one - without the
             * bit the driver ignores the extension block entirely, and with it
             * the superindex. MODE_LONGFN so that entries with names longer than
             * 31 characters are not thrown away by the directory pass that comes
             * after this rebuild; see BuildRext() for the size.
             */
            rbl.Options = Constants.MODE_HARDDISK | Constants.MODE_SPLITTED_ANODES | Constants.MODE_DIR_EXTENSION |
                          Constants.MODE_SIZEFIELD | Constants.MODE_DATESTAMP | Constants.MODE_EXTROVING |
                          Constants.MODE_EXTENSION | Constants.MODE_LONGFN;

            // #if LARGE_FILE_SIZE
            // rbl->options |= MODE_LARGEFILE;
            // rbl->disktype = ID_PFS2_DISK;
            // #endif

            /* the size field holds the partition size in native sectors, which
             * is what the driver stores and what CheckRootBlock() compares
             * against; volume.disksize counts logical blocks
             */
            rbl.DiskSize = volume.FullDiskSizeNative;

            /* Choose the reserved block size, and with it the format, the way
             * the driver does when it formats a volume: the anode index needs
             * superblocks past MAXSMALLDISK, and larger reserved blocks past the
             * 1K and 2K bitmap limits. Deciding this from rbl->disktype instead
             * could never do anything, because it is assigned ID_PFS_DISK a few
             * lines up - which left this able to rebuild only the pre-20.0
             * format, and to reject anything over 104GB outright.
             */
            resblocksize = 1024;
            if (volume.DiskSize > DoctorConstants.MAXSMALLDISK)
            {
                rbl.Options |= Constants.MODE_SUPERINDEX;
                if (volume.DiskSize > DoctorConstants.MAXDISKSIZE1K)
                {
                    resblocksize = 2048;
                    if (volume.DiskSize > DoctorConstants.MAXDISKSIZE2K)
                        resblocksize = 4096;
                }
            }

            /* a reserved block is never smaller than a logical block */
            if (volume.BlockSize > resblocksize)
            {
                resblocksize = volume.BlockSize;
            }

            /* reserved blocks over 1024 bytes, or logical blocks over 512, mean
             * the new on-disk format
             */
            if (resblocksize > 1024 || volume.BlockLogShift != 0)
            {
                rbl.DiskType = Constants.ID_PFS2_DISK;
            }

            if (rbl.DiskType == Constants.ID_PFS2_DISK)
            {
                if (volume.DiskSize > DoctorConstants.MAXDISKSIZE4K)
                {
                    AddError("Too large (>1.6TB) partition size");
                    return Error.OptionsError;
                }
            }
            else
            {
                if (volume.DiskSize > DoctorConstants.MAXDISKSIZE1K)
                {
                    AddError("Too large (>104GB) partition size");
                    return Error.OptionsError;
                }
            }

            volume.ResCluster = resblocksize / volume.BlockSize;
            rbl.ReservedBlksize = (ushort)resblocksize;

            rbl.Datestamp = ~0U; // don't be a break on search functions
            rbl.CreationDay = (ushort)time.Days;
            rbl.CreationMinute = (ushort)time.Minutes;
            rbl.CreationTick = (ushort)time.Ticks;
            rbl.Protection = 0xf0;
            rbl.FirstReserved = 2;
            rbl.LastReserved = await SearchLastReserved(volume);
            volume.Progress(0, 1);
            rbl.ReservedFree = (rbl.LastReserved - rbl.FirstReserved) / volume.ResCluster;

            /* blocks 0 .. lastreserved are taken by the reserved area, so the
             * free count is disksize - lastreserved - 1. volume.disksize counts
             * the blocks that really exist between the partition limits; using
             * the raw partition size handed out a block past the end.
             */
            rbl.BlocksFree = volume.DiskSize - rbl.LastReserved - 1;
            rbl.AlwaysFree = rbl.BlocksFree / 20;

            rbl.DiskName = "FixedDisk";

            /* extension is created by build extension */

            /* create bitmap index */
            volume.Status(1, "Searching bitmap blocks", rbl.LastReserved - 2);
            await SearchBlocks(ind, 0, Constants.MAXBITMAPINDEX, rbl.FirstReserved, rbl.LastReserved,
                Constants.BMIBLKID);
            volume.Progress(0, 1);
            if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
                j = Constants.MAXBITMAPINDEX + 1;
            else
                j = Constants.MAXSMALLBITMAPINDEX + 1;

            for (i = 0; i < j; i++)
                rbl.IdxLargeBitmapIndex(i).Value = ind[i].BlockNr;

            /* create anodeindex */
            if ((rbl.Options & Constants.MODE_SUPERINDEX) == 0)
            {
                ind = NewScanElements(Constants.MAXBITMAPINDEX + 1);
                volume.Status(1, "Searching index blocks", rbl.LastReserved - 2);
                await SearchBlocks(ind, 0, Constants.MAXSMALLINDEXNR, rbl.FirstReserved, rbl.LastReserved,
                    Constants.IBLKID);
                for (i = 0; i < Constants.MAXSMALLINDEXNR + 1; i++)
                    rbl.IdxSmallIndexBlocks(i).Value = ind[i].BlockNr;
            }

            rbl.Datestamp = maxdatestamp + 0x200;
            volume.Progress(0, 1);
            volume.Status(1, " ", 100);
            return Error.None;
        }

        /// <summary>
        /// Create preallocated and cleared array of scanelement.
        /// </summary>
        private static ScanElement[] NewScanElements(uint size)
        {
            var el = new ScanElement[size];
            for (var i = 0; i < size; i++)
                el[i] = new ScanElement();
            return el;
        }

        /* Search deldirblocks, indexblocks, anodeblocks, bitmapblocks
         * el = preallocated and cleared array of scanelement for storing result
         * seqlow = lowest element array
         * seqhigh = highest element array
         * start = first blocknr
         * stop = last blocknr
         */
        private async Task SearchBlocks(ScanElement[] el, uint seqlow, uint seqhigh,
            uint start, uint stop, ushort bloktype)
        {
            uint i, seqnr, ds;

            var blk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            volume.Status(1, null, (stop - start) / volume.ResCluster);
            for (i = start; i < stop; i += volume.ResCluster)
            {
                volume.Progress(1, 1);
                if (await VolGetBlock(blk, i) != Error.None)
                    continue;

                if (aborting)
                {
                    volume.ShowMsg("Aborting block search\n");
                    aborting = false;
                    break;
                }

                if (blk.Id == bloktype)
                {
                    seqnr = blk.IndexBlock.SeqNr;
                    ds = blk.IndexBlock.Datestamp;
                    if (seqnr >= seqlow && seqnr <= seqhigh &&
                        ds >= el[seqnr - seqlow].Datestamp)
                    {
                        el[seqnr - seqlow].BlockNr = i;
                        el[seqnr - seqlow].Datestamp = ds;
                        maxdatestamp = Math.Max(ds, maxdatestamp);
                    }
                }
            }

            volume.Status(1, " ", 100);
        }

        /* search for a block on a volume.
         * bloktype - kind of block to look for
         * seqnr - longword on offset 8 (not for REXT)
         * last - blocknr. Max is last + 32
         * datestamp - maximum datestamp on offset 4, 0 = no max
         * anodenr, parent - for dirblocks only, 0 = ignore
         * returns bloknr
         *
         * bloktypes: REXT, BM, BMI, S, AI, A, D
         */
        private async Task<uint> SearchBlock(ushort bloktype, uint seqnr, uint last, uint datestamp, uint anodenr,
            uint parent)
        {
            uint ds, tmp;
            uint i, bloknr;
            var found = 0;

            var blk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            bloknr = ds = 0;
            if (last != 0)
            {
                last += 64;
                last = Math.Min(rbl.LastReserved, last);
            }
            else
                last = rbl.LastReserved;

            last -= (last % volume.ResCluster);

            if (datestamp == 0)
                datestamp = rbl.Datestamp;

            volume.Status(1, null, 1024);
            for (i = last; i != last + volume.ResCluster; i -= volume.ResCluster)
            {
                /* rollover to end of reserved area */
                if (i < rbl.FirstReserved)
                {
                    if (last >= rbl.LastReserved - 4)
                        break;
                    i = rbl.LastReserved & ~(volume.ResCluster - 1);
                }

                /* break if nothing found for 128 blocks, and already found promising
                 * candidate. Uses roundrobin allocation
                 */
                if (found != 0 && --found == 0)
                    break;

                volume.Progress(1, 1);
                if (await VolGetBlock(blk, i) != Error.None)
                    continue;

                if (aborting)
                {
                    // break search, don't break repair
                    volume.ShowMsg("Aborting block search\n");
                    aborting = false;
                    break;
                }

                if (blk.Id == bloktype)
                {
                    if (blk.IndexBlock.SeqNr != seqnr && bloktype != Constants.EXTENSIONID)
                        continue;

                    if (anodenr != 0 && (blk.DirBlock.AnodeNr != anodenr ||
                                         blk.DirBlock.Parent != parent))
                        continue;

                    if (datestamp != 0 && blk.DirBlock.Datestamp > datestamp)
                        continue;

                    /* found block */
                    tmp = (bloktype == Constants.EXTENSIONID)
                        ? blk.ExtensionBlock.Datestamp
                        : blk.DirBlock.Datestamp;

                    if (tmp >= ds)
                    {
                        if (datestamp != 0)
                        {
                            if (tmp > datestamp)
                                continue;

                            /* found a promising candidate ? */
                            if (datestamp - tmp < 100)
                                found = 512; // 1.3: was 128
                        }

                        ds = tmp;
                        bloknr = i;
                    }
                }
            }

            volume.Status(1, " ", 100);
            return bloknr;
        }

        /* Get last reserved block */
        private async Task<uint> SearchLastReserved(Volume vol)
        {
            uint i, last, cdwn;

            var blk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            cdwn = 4096;
            i = last = vol.DiskSize / 256;
            i -= i % volume.ResCluster;
            vol.Status(1, "Scanning disk", vol.DiskSize / 16 - last);
            while (i < vol.DiskSize / 16)
            {
                vol.Progress(1, 1);
                if (aborting)
                {
                    volume.ShowMsg("Aborting filesystem search\n");
                    aborting = false;
                    break;
                }

                if (await VolGetBlock(blk, i) != Error.None)
                    break;

                var exit = false;
                switch (blk.Id)
                {
                    case Constants.DBLKID:
                    case Constants.ABLKID:
                    case Constants.IBLKID:
                    case Constants.BMBLKID:
                    case Constants.BMIBLKID:
                    case Constants.DELDIRID:
                    case Constants.EXTENSIONID:
                    case Constants.SBLKID:

                        cdwn = 1024;
                        last = i;
                        break;

                    default:

                        /* Exit if blocks not reserved */
                        if (cdwn-- == 0)
                            exit = true;
                        break;
                }

                if (exit)
                    break;

                i += volume.ResCluster;
            }

            vol.Status(1, " ", 100);
            return last;
        }

        /* Search filesystem on specified execdevice/unit. Start at startblok,
         * end at endblok
         */
        private async Task<uint> SearchFileSystem(int startblok, int endblok)
        {
            uint blnr = 0, b;

            var rbl = new RootBlock(new byte[DoctorConstants.MAXRESBLOCKSIZE]);

            volume.Status(0, "Searching filesystem", endblok - startblok);
            startblok = Math.Max(startblok, 0);
            startblok &= ~1; /* even bloknr */
            for (b = (uint)startblok; b < endblok; b += Math.Max(volume.ResCluster, 1))
            {
                volume.Progress(0, 1);
                if (aborting)
                {
                    volume.ShowMsg("Aborting filesystem search\n");
                    aborting = false;
                    break;
                }

                // read block and check if it is a rootblock */
                if (await CGetBlock(rbl.Data, b, volume.BlockSize) != Error.None)
                    break;

                if (IsRootBlock(rbl))
                {
                    blnr = b;
                    break;
                }
            }

            return blnr;
        }

        /* cached block structure is managed by caller
         * block data is managed by this function
         * creates an extension block with uptodate reference to
         * superblock and no deldir.
         */
        private async Task<Error> BuildRext(CachedBlock rext)
        {
            int i;

            volume.Status(1, "Building rext", (rbl.LastReserved - rbl.FirstReserved) / volume.ResCluster);

            rext.Data = new byte[SizeOfResBlock];
            var r = rext.ExtensionBlock;
            var time = Amiga.DateHelper.ConvertToAmigaDate(DateTime.Now);

            r.Id = Constants.EXTENSIONID;
            r.Datestamp = rbl.Datestamp;
            r.Pfs2Version = (17 << 16) + 99;
            /* MODE_LONGFN is set on the rebuilt rootblock, so this has to hold a
             * size the driver accepts (< FNSIZE) and pfsdoctor considers legal
             * (30..108). The original size cannot be recovered, so take the
             * largest one: a smaller value would make the directory pass delete
             * every entry whose name is longer than it.
             */
            r.FnSize = DoctorConstants.FNSIZE - 1;
            r.SetRootDate(0, (ushort)time.Days);
            r.SetRootDate(1, (ushort)time.Minutes);
            r.SetRootDate(2, (ushort)time.Ticks);

            volume.Progress(1, 1);
            if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
            {
                var ind = NewScanElements(Constants.MAXSUPER + 1);
                await SearchBlocks(ind, 0, Constants.MAXSUPER, rbl.FirstReserved, rbl.LastReserved,
                    Constants.SBLKID);
                for (i = 0; i < 16; i++)
                    r.SuperIndex((uint)i).Value = ind[i].BlockNr;
            }

            /* add to list */
            var bbl = new CachedBlock
            {
                BlockNr = rext.BlockNr = ~0U,
                Mode = rext.Mode = Mode.Build,
                Data = rext.Data
            };
            AddBuildBlockNode("BuildRext", bbl);

            volume.Status(1, " ", 100);
            return Error.None;
        }

        /* zet mode to build
         */
        private async Task<Error> BuildIndexBlock(CachedBlock blk, ushort bloktype, uint seqnr)
        {
            uint i;
            ushort childtype;

            volume.Status(1, "Building indexblock", IndexPerBlock);
            var ind = NewScanElements(IndexPerBlock);

            blk.Data = new byte[SizeOfResBlock];
            var ib = blk.IndexBlock;

            ib.Id = bloktype;
            ib.Datestamp = rbl.Datestamp;
            ib.SeqNr = seqnr;

            /* fill index */
            switch (bloktype)
            {
                case Constants.IBLKID:
                    childtype = Constants.ABLKID;
                    break;
                case Constants.BMIBLKID:
                    childtype = Constants.BMBLKID;
                    break;
                case Constants.SBLKID:
                    childtype = Constants.IBLKID;
                    break;
                default: return Error.FatalError;
            }

            await SearchBlocks(ind, seqnr * IndexPerBlock, seqnr * IndexPerBlock + IndexPerBlock - 1,
                rbl.FirstReserved, rbl.LastReserved, childtype);
            for (i = 0; i < IndexPerBlock; i++)
                ib.Index(i).Value = ind[i].BlockNr;

            /* add to list */
            var bbl = new CachedBlock
            {
                BlockNr = blk.BlockNr = ~0U,
                Mode = blk.Mode = Mode.Build,
                Data = blk.Data
            };
            AddBuildBlockNode("BuildIndexBlock", bbl);

            volume.Status(1, " ", 100);
            return Error.None;
        }

        private Error BuildBitmapBlock(CachedBlock blk, uint seqnr)
        {
            uint i;

            volume.Status(1, "Building bitmapblock", 100);

            /* the buffer is SIZEOF_RESBLOCK, not one logical block */
            blk.Data = new byte[SizeOfResBlock];
            var bmb = blk.BitmapBlock;

            bmb.Id = Constants.BMBLKID;
            bmb.Datestamp = rbl.Datestamp;
            bmb.SeqNr = seqnr;

            /* fill bitmap */
            for (i = 0; i < LongsPerBmb; i++)
                bmb.Bitmap(i).Value = ~0U;

            /* add to list */
            var bbl = new CachedBlock
            {
                BlockNr = blk.BlockNr = ~0U,
                Mode = blk.Mode = Mode.Build,
                Data = blk.Data
            };
            AddBuildBlockNode("BuildBitmapBlock", bbl);

            volume.Status(1, " ", 100);
            return Error.None;
        }
    }
}
