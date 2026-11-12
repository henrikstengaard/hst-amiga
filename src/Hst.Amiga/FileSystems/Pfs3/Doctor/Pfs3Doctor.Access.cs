namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Port of access.c. Non-checking functions for access to structures on PFS3 disks.
    /// </summary>
    public partial class Pfs3Doctor
    {
        /* get a buildblock from the buildblock cache
         */
        private CachedBlock GetBuildBlock(ushort bloktype, uint seqnr)
        {
            foreach (var bbl in volume.BuildBlocks)
            {
                if (bbl.Id == bloktype &&
                    bbl.IndexBlock.SeqNr == seqnr)
                {
                    return bbl;
                }
            }

            return null;
        }

        /* get reserved block
         * anything, except deldir, root, boot, dir and rext
         */
        private async Task<Error> GetResBlock(CachedBlock blok, ushort bloktype, uint seqnr, bool fix)
        {
            CachedBlock t;
            uint blknr, index, offset;
            UInt32Pointer bp = null;
            Error error;

            // controleer build block lijst
            t = GetBuildBlock(bloktype, seqnr);
            if (t != null)
            {
                blok.BlockNr = t.BlockNr;
                blok.Mode = t.Mode;
                Array.Copy(t.Data, blok.Data, Math.Min(t.Data.Length, blok.Data.Length));
                return Error.None;
            }

            var blk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            index = seqnr / IndexPerBlock;
            offset = seqnr % IndexPerBlock;
            switch (bloktype)
            {
                case Constants.SBLKID:

                    if (seqnr > Constants.MAXSUPER)
                    {
                        return Error.NumberError;
                    }

                    bp = rext.ExtensionBlock.SuperIndex(seqnr);
                    if (bp.Value == 0)
                    {
                        if (fix)
                        {
                            AddError("superindex block not found");
                            AddError($"seq = {seqnr} index = {index} offset = {offset} {bloktype:x8}");
                            error = await RepairSuperIndex(bp, seqnr);
                            if (error != Error.None)
                                bp.Value = 0;
                            else
                            {
                                await VolWriteBlock(rext);
                                KillAnodeBitmap();
                            }
                        }
                    }

                    break;

                case Constants.BMIBLKID:

                    /* seqnr is derived from on-disk data and indexes a fixed
                     * size array in the rootblock, so it must be range checked
                     * like the SBLKID case above. Without this a corrupt seqnr
                     * read (and, in fix mode, wrote) past the end of the array.
                     */
                    if (seqnr > Constants.MAXBITMAPINDEX)
                    {
                        return Error.NumberError;
                    }

                    bp = rbl.IdxLargeBitmapIndex(seqnr);
                    if (bp.Value == 0)
                    {
                        if (fix)
                        {
                            AddError("bitmapindex block not found");
                            AddError($"seq = {seqnr} index = {index} offset = {offset} {bloktype:x8}");
                            error = await RepairBitmapIndex(bp, seqnr);
                            if (error != Error.None)
                                bp.Value = 0;
                            else
                                await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                    volume.BlockSize);
                        }
                    }

                    break;

                case Constants.IBLKID:

                    if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
                    {
                        error = await GetResBlock(blk, Constants.SBLKID, index, fix);
                        if (error != Error.None)
                        {
                            return error;
                        }

                        bp = blk.IndexBlock.Index(offset);
                    }
                    else
                    {
                        /* same range check as the driver's GetAnodeBlock() */
                        if (seqnr > Constants.MAXSMALLINDEXNR)
                        {
                            return Error.NumberError;
                        }

                        bp = rbl.IdxSmallIndexBlocks(seqnr);
                    }

                    if (bp.Value == 0)
                    {
                        if (fix)
                        {
                            AddError("anodeindex block not found");
                            AddError($"seq = {seqnr} index = {index} offset = {offset} {bloktype:x8}");
                            error = await RepairAnodeIndex(bp, seqnr);
                            if (error != Error.None)
                                bp.Value = 0;
                            else
                            {
                                /* the anodebitmap, which is made per aib,
                                 * could be too small
                                 */
                                KillAnodeBitmap();
                                if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
                                    await VolWriteBlock(blk);
                                else
                                    await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                        volume.BlockSize);
                            }
                        }
                    }

                    break;

                case Constants.ABLKID:

                    error = await GetResBlock(blk, Constants.IBLKID, index, fix);
                    if (error != Error.None)
                    {
                        return error;
                    }

                    bp = blk.IndexBlock.Index(offset);
                    if (bp.Value == 0)
                    {
                        if (fix)
                        {
                            AddError("anode block not found");
                            AddError($"seq = {seqnr} index = {index} offset = {offset} {bloktype:x8}");
                            // RepairAnodeBlock already called from RepairAnodeTree
                            // Pointless to call it again here.
                            //if (error = RepairAnodeBlock(bp, seqnr))
                            //	*bp = 0;
                            //else
                            //	volume.writeblock((cachedblock_t *)&blk);
                        }
                    }

                    break;

                case Constants.BMBLKID:

                    error = await GetResBlock(blk, Constants.BMIBLKID, index, fix);
                    if (error != Error.None)
                    {
                        return error;
                    }

                    bp = blk.IndexBlock.Index(offset);
                    if (bp.Value == 0)
                    {
                        if (fix)
                        {
                            AddError("bitmap block not found");
                            AddError($"seq = {seqnr} index = {index} offset = {offset} {bloktype:x8}");
                            error = await RepairBitmapBlock(bp, seqnr);
                            if (error != Error.None)
                                bp.Value = 0;
                            else
                                await VolWriteBlock(blk);
                        }
                    }

                    break;

                default:
                    // not in original code, unknown block type would dereference a null pointer
                    return Error.NotFound;
            }

            blknr = bp.Value;

            if (blknr == 0)
                return Error.NotFound;

            error = await VolGetBlock(blok, blknr);
            if (error != Error.None)
                return error;

            return Error.None;
        }

        /// <summary>
        /// Anode read cache (tablk in access.c).
        /// </summary>
        private readonly CachedBlock tablk;

        /// <summary>
        /// Anode read cache data (tanodedata in access.c).
        /// </summary>
        private readonly byte[] tanodedata = new byte[DoctorConstants.MAXRESBLOCKSIZE];

        private async Task<bool> GetAnode(CAnode anode, uint anodenr, bool fix)
        {
            // anodenr_t *split = (anodenr_t *)&anodenr;
            var splitSeqNr = anodenr >> 16;
            var splitOffset = anodenr & 0xffff;
            var tanodeblk = new AnodeBlock(tanodedata);

            if (!(tablk.Data != null && tanodeblk.SeqNr == splitSeqNr))
            {
                tablk.Data = tanodedata;
                if (await GetResBlock(tablk, Constants.ABLKID, splitSeqNr, fix) != Error.None)
                {
                    tablk.Data = null;
                    return false;
                }
            }

            /* slot ANODES_PER_BLOCK is one past the last one in the block */
            if (splitOffset >= AnodesPerBlock)
                return false;

            var node = tablk.AnodeBlock.Nodes(splitOffset);
            anode.Nr = anodenr;
            anode.ClusterSize = node.ClusterSize;
            anode.BlockNr = node.BlockNr;
            anode.Next = node.Next;
            return true;
        }

        private async Task<bool> SaveAnode(CAnode anode, uint nr)
        {
            // anodenr_t *split = (anodenr_t *)&nr;
            var splitSeqNr = nr >> 16;
            var splitOffset = nr & 0xffff;

            tablk.Data = null; /* kill anode read cache */
            var ablk = new CachedBlock { Data = new byte[DoctorConstants.MAXRESBLOCKSIZE] };
            if (await GetResBlock(ablk, Constants.ABLKID, splitSeqNr, false) != Error.None)
                return false;

            /* slot ANODES_PER_BLOCK is one past the last one in the block: with
             * 4096 byte reserved blocks it lands exactly on the end of 'buffer'
             */
            if (splitOffset >= AnodesPerBlock)
                return false;

            var node = ablk.AnodeBlock.Nodes(splitOffset);
            node.ClusterSize = anode.ClusterSize;
            node.BlockNr = anode.BlockNr;
            node.Next = anode.Next;
            await VolWriteBlock(ablk);
            return true;
        }

        private uint GetDEFileSize(DirEntry direntry, ExtraFields extra, out uint high)
        {
            high = 0;
            if ((rbl.Options & Constants.MODE_LARGEFILE) != 0)
                high = extra.FSizeX;
            return direntry.FSize;
        }

        private uint GetDDFileSize(DelDirEntry dde, out uint high)
        {
            high = 0;
            if ((rbl.Options & Constants.MODE_LARGEFILE) != 0 && dde.FilenameLength <= DoctorConstants.DELENTRYFNSIZE)
            {
                high = dde.FSizeX;
                return dde.FSize;
            }

            return dde.FSize;
        }
    }
}
