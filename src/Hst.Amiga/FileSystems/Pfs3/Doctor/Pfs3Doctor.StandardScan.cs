namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Port of standardscan.c.
    /// </summary>
    public partial class Pfs3Doctor
    {
        /**************************************
         * Globals
         **************************************/

        private readonly ScanState ss;

        /// <summary>
        /// rootblock (rootblock_t *rbl)
        /// </summary>
        private RootBlock rbl;

        /// <summary>
        /// rootblock extension (c_extensionblock_t rext)
        /// </summary>
        private readonly CachedBlock rext;

        private bool redosyntax;

        private bool FileSizeCheck(uint low, uint high, uint numblocks)
        {
            uint size, oldlow;

            oldlow = low;
            low += volume.BlockSize - 1;
            if (low < oldlow)
                high++;
            size = low >> volume.BlockShift;
            size |= high << (32 - volume.BlockShift);
            if (size == numblocks)
                return true;
            return false;
        }

        /**************************************
         * StandardScan						L1
         **************************************/

        // public interface
        public async Task<Error> StandardScan(StandardScanFlags opties)
        {
            StandardScanFlags flags;

            aborting = false;
            ss.Flags = StandardScanFlags.None;
            ss.Opties = opties;
            ss.Pass = 0;
            ss.Verbose = false;
            ss.Unformat = false;
            ss.Info = false;
            ss.Stage = Stage.Syntax;

            flags = opties;
            if ((opties & (StandardScanFlags.Check | StandardScanFlags.Fix)) != 0)
                flags |= StandardScanFlags.GenBmMask;

            /* init stats */
            stats = new Stats();
            return await MainStandardScan(flags);
        }

        // core standardscan
        private async Task<Error> MainStandardScan(StandardScanFlags flags)
        {
            Error error;

            volume.ShowMsg("Initializing\n");
            ss.Flags = flags;
            ss.Stage = Stage.Syntax;
            ss.Pass = stats.Pass = 1;
            ss.Verbose = (flags & StandardScanFlags.Verbose) != 0;
            ss.Unformat = (flags & StandardScanFlags.Unformat) != 0;
            ss.Info = (flags & StandardScanFlags.Info) != 0;

            InitFullScan();

            /* unformat .. */
            if (ss.Unformat)
            {
                mode = Mode.Repair; // !! niet really correct !! fix this
                volume.ShowMsg("Unformatting...\n");
                rbl = new RootBlock(new byte[DoctorConstants.MAXRESBLOCKSIZE]);
                if ((error = await BuildRootBlockHub()) != Error.None || aborting)
                    return await ExitStandardScan(error);
            }

            /* read rootblock */
            volume.Status(0, "rootblock", 100);
            if ((error = await GetRootBlock()) != Error.None || aborting)
                return await ExitStandardScan(error);

            // not in original code, info mode continues without a valid rootblock
            if (ss.Info && !IsRootBlock(rbl))
                return await ExitStandardScan(Error.RblNotFound);

            volume.Progress(0, 40);
            if ((error = await RepairBootBlock()) != Error.None || aborting)
                return await ExitStandardScan(error);

            /* after the rootblock and bootblock the boundaries of the partition are known
             */

            /* read rootblockextension */
            volume.Progress(0, 30);
            rext.Data = new byte[SizeOfResBlock];
            rext.Mode = Mode.Check;
            if ((error = await GetRext()) != Error.None || aborting)
                return await ExitStandardScan(error);

            if (GetPFS2Revision(out var bericht))
            {
                volume.ShowMsg("Disk formatted with ");
                volume.ShowMsg(bericht);
                volume.ShowMsg("\n");
            }

            if (ss.Info)
            {
                await ExitStandardScan(Error.None);
                return Error.None;
            }

            /* stage 1 */
            volume.Progress(0, 30);
            while (ss.Stage != Stage.Finished && ss.Pass < DoctorConstants.MAX_PASS)
            {
                stats.Pass = ss.Pass;
                volume.ShowMsg($"Starting pass {ss.Pass}\n");
                volume.UpdateStats();
                if ((mode == Mode.Check || mode == Mode.Info) && stats.NumErrors > 0)
                    break;
                error = await SsCheckDirTree();
                if ((error != Error.None) || aborting)
                    return await ExitStandardScan(error);
                if (mode == Mode.Check || mode == Mode.Info)
                    break;
                ss.Pass++;
            }

            if (ss.Stage != Stage.Finished)
            {
                // original code returns without exit standard scan, which leaves dirty cache lines unwritten
                await ExitStandardScan(Error.MaxPassExceeded);
                return Error.MaxPassExceeded;
            }

            volume.Status(0, "finishing up", 2);
            volume.Progress(0, 1);
            await ExitStandardScan(Error.None);
            volume.Progress(0, 1);
            return Error.None;
        }

        private async Task<Error> ExitStandardScan(Error error)
        {
            await UpdateCache();

            rbl = null;

            rext.Data = null;

            KillReservedBitmap();
            KillAnodeBitmap();
            KillMainBitmap();
            ExitFullScan();

            if ((error != Error.None) || aborting)
                volume.ShowMsg("ABORTED\n");
            else if (ss.Unformat)
            {
                volume.AskUser(
                    "Disk has been unformatted. Unformatting can\n" +
                    "cause problems later. Therefor we recommend\n" +
                    "to format this partition after backing up  \n" +
                    "all data\n", "OK", null);
            }

            return error;
        }

        /* main loop: check the partition defined by the rootblock and
         * rext (rootblock extension)
         */
        private async Task<Error> SsCheckDirTree()
        {
            Error error;
            var newpassneeded = false;

            ClearStats();
            redosyntax = false;

            /* fall through stages as switch in original code */
            if (ss.Stage == Stage.Syntax || ss.Stage == Stage.ResBitmap)
            {
                /* initialize reserved bitmap generation */
                if ((ss.Flags & StandardScanFlags.GenResBitmap) != 0)
                    await InitReservedBitmap();

                if (rext.Mode == Mode.Check)
                    if ((error = ResBlockUsed(rext.BlockNr)) != Error.None)
                        return error;

                /* anode blok tree */
                /* if anodeindexblok faulty --> create (using DiskScan) */
                if ((error = await RepairAnodeTree()) != Error.None || aborting)
                    return error;

                /* bitmap */
                /* check syntax bitmap tree
                 * if block faulty --> create, kill bitmap, ss_makebm = false
                 * and continue */
                if ((error = await RepairBitmapTree()) != Error.None || aborting)
                    return error;
            }

            if (ss.Stage <= Stage.MainBitmap)
            {
                if ((ss.Flags & StandardScanFlags.GenMainBitmap) != 0)
                    InitMainBitmap();
            }

            if (ss.Stage <= Stage.AnodeBitmap)
            {
                if ((ss.Flags & StandardScanFlags.GenAnodeBitmap) != 0)
                    await InitAnodeBitmap();
            }

            /* deldir */
            if ((error = await RepairDeldir()) != Error.None || aborting)
                return error;

            /* directory tree */
            if ((error = await RepairDirTree()) != Error.None || aborting)
                return error;

            /* on hardlink conflict */
            if (redosyntax)
            {
                ss.Stage = Stage.Syntax;
                return Error.None;
            }

            switch (ss.Stage)
            {
                case Stage.Syntax:
                case Stage.ResBitmap:

                    /* need another pass if blocks were created
                     * (should not be necessary!!)
                     */
                    if (volume.BuildBlocks.Count > 0)
                        newpassneeded = true;

                    /* allocating created blocks & reparing reserved bitmap can only
                     * be done after the reserved bitmap has been generated.
                     * ss.flags is set to false if generation was aborted.
                     */
                    if (IsBitmapValid(volume.ResBitmap))
                    {
                        if ((error = await AllocBuildBlocks()) != Error.None)
                            return error;

                        if ((error = await RepairReservedBitmap()) != Error.None || aborting)
                            return error;
                    }
                    else
                    {
                        if (ss.Stage < Stage.ResBitmap)
                        {
                            ss.Stage = Stage.ResBitmap;
                            break;
                        }

                        return Error.ResBitmapFail; /* fatal error */
                    }

                    if (newpassneeded)
                        break;

                    goto case Stage.MainBitmap;

                case Stage.MainBitmap:
                case Stage.AnodeBitmap:

                    /* bitmap generatie has been aborted (disabled during scan)
                     * generate main and anodebitmap
                     */
                    if (!IsBitmapValid(volume.MainBitmap))
                    {
                        if (ss.Stage < Stage.MainBitmap)
                        {
                            ss.Stage = Stage.MainBitmap;
                            break;
                        }

                        return Error.MainBitmapFail; /* fatal error */
                    }

                    if (!IsBitmapValid(volume.AnodeBitmap))
                    {
                        if (ss.Stage < Stage.AnodeBitmap)
                        {
                            ss.Stage = Stage.AnodeBitmap;
                            break;
                        }

                        return Error.AnodeBitmapFail;
                    }

                    if ((error = await RepairMainBitmap()) != Error.None || aborting)
                        return error;

                    if ((error = await RepairAnodeBitmap()) != Error.None || aborting)
                        return error;

                    ss.Stage = Stage.Finished;
                    break;

                default:
                    // case doubledel:

                    ss.Stage = Stage.MainBitmap;
                    break;
            }

            /* Need another pass if blocks build (created) during
             * scan. Calling allocbuildblocks here is pointless;
             * the reserved bitmap is not valid because of invalid blocks,
             * possibly during non reserved bitmap phase!
             */
            if (volume.BuildBlocks.Count > 0)
                ss.Stage = Stage.ResBitmap;

            return Error.None;
        }

        /**************************************
         * Volume functions
         **************************************/

        /* functions for in cache struct
         * gets block from reserved area and checks partition
         * borders etc.
         * status/errors:
         * read/write errors
         * e_none
         * block_outside_reserved
         * block_outside_partition
         */

        /* check if a block is in partition range */
        private Error VolCheckBlockNr(ref uint blocknr)
        {
            if (blocknr > volume.LastReserved)
            {
                AddError("block outside reserved area");
                return Error.BlockOutsideReserved;
            }

            blocknr += volume.FirstBlock;
            if (blocknr > volume.LastBlock || blocknr < volume.FirstBlock)
            {
                AddError("block outside partition");
                return Error.BlockOutsidePartition;
            }

            return Error.None;
        }

        /// <summary>
        /// Get reserved block (vol_GetBlock, volume.getblock in original code).
        /// </summary>
        private async Task<Error> VolGetBlock(CachedBlock blok, uint bloknr)
        {
            Error error;
            uint realbloknr;

            realbloknr = bloknr;
            if ((error = VolCheckBlockNr(ref realbloknr)) != Error.None)
                return error;

            if ((error = await CGetBlock(blok.Data, realbloknr, SizeOfResBlock)) != Error.None)
            {
                return error;
            }

            blok.Mode = Mode.Check;
            blok.BlockNr = bloknr;
            return Error.None;
        }

        /// <summary>
        /// Write reserved block (vol_WriteBlock, volume.writeblock in original code).
        /// </summary>
        private async Task<Error> VolWriteBlock(CachedBlock blok)
        {
            Error status;
            uint bloknr;

            if (blok.Mode != Mode.Build)
            {
                bloknr = blok.BlockNr;
                if ((status = VolCheckBlockNr(ref bloknr)) != Error.None)
                    return status;

                return await CWriteBlock(blok.Data, bloknr, SizeOfResBlock);
            }

            return Error.None;
        }

        /**************************************
         * GetRootBlock
         **************************************/

        // public functions

        private async Task<Error> BuildRootBlockHub()
        {
            Error error;

            if (volume.AskUser(
                    "Press ok to rebuild rootblock or cancel to exit\n" +
                    "WARNING: make sure correct accessmode (direct-scsi/td64/nsd)\n" +
                    "is selected", "ok", "cancel"))
            {
                if ((error = await BuildRootBlock(rbl)) == Error.None)
                {
                    FixedError("new rootblock build");
                    await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock, volume.BlockSize);
                }
            }
            else
                error = Error.Aborted;

            return error;
        }

        /// <summary>
        /// Write mountfile, replaces ASL file requester in original code.
        /// </summary>
        private void WriteMountFile()
        {
            var ok = false;
            do
            {
                var stream = volume.OpenMountFile();
                if (stream != null)
                {
                    if ((ok = MakeMountFile(stream)))
                        volume.ShowMsg("Mountlist written\n");
                }
                else
                {
                    volume.ShowMsg("no mountlist written\n");
                    // original code retries until mountlist is written
                    break;
                }
            } while (!ok);
        }

        /* read from disk and check
         * Returns error (0 = ERROR_NONE = ok).
         */
        private async Task<Error> GetRootBlock()
        {
            Error error;
            uint bloknr;
            bool okuser;

            EnterBlock(DoctorConstants.ROOTBLOCK);
            volume.ShowMsg(
                $"Checking rootblock {(DoctorConstants.ROOTBLOCK + volume.FirstBlock) << volume.BlockLogShift}\n");

            rbl = new RootBlock(new byte[DoctorConstants.MAXRESBLOCKSIZE]);

            // read rootblock
            if ((error = await CGetBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                    volume.BlockSize)) != Error.None)
            {
                AddError("rootblock could not be loaded");
                return error;
            }

            if (!IsRootBlock(rbl) && volume.BlockLogShift != 0)
            {
                var rootFound = false;
                // Check if we have pre-20.0 pfs3 that has ignored >512 block size
                await InitCache(0, 0, volume.BlockSize >> volume.BlockLogShift);
                volume.BlockSize >>= volume.BlockLogShift;
                volume.BlockShift -= volume.BlockLogShift;
                if (await CGetBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlockNative,
                        volume.BlockSize) == Error.None)
                {
                    if (IsRootBlock(rbl))
                    {
                        volume.ShowMsg(
                            $"Rootblock {DoctorConstants.ROOTBLOCK + volume.FirstBlockNative} found using 512 block size (block size was {volume.BlockSize << volume.BlockLogShift})\n");
                        var cylsectors = volume.DosEnvec.de_Surfaces * volume.DosEnvec.de_BlocksPerTrack;
                        volume.BlockLogShift = 0;
                        volume.FirstBlockNative = volume.DosEnvec.de_LowCyl * cylsectors;
                        /* saturate instead of wrapping at 2^32 sectors, see OpenVolume() */
                        if (cylsectors != 0 && (volume.DosEnvec.de_HighCyl + 1) > 0xFFFFFFFFUL / cylsectors)
                            volume.LastBlockNative = 0xFFFFFFFF;
                        else
                            volume.LastBlockNative = (volume.DosEnvec.de_HighCyl + 1) * cylsectors;
                        volume.FullDiskSizeNative = volume.LastBlockNative - volume.FirstBlockNative;
                        volume.FirstBlock = volume.FirstBlockNative;
                        volume.LastBlockNative--;
                        volume.LastBlock = volume.LastBlockNative;
                        /* same definition as in OpenVolume(): the blocks between
                         * the partition limits. volume.disksizenative was
                         * computed with the discarded blocklogshift.
                         */
                        volume.DiskSize = volume.LastBlock - volume.FirstBlock + 1;
                        volume.LastReserved = volume.DiskSize - 256;
                        rootFound = true;
                    }
                }

                if (!rootFound)
                {
                    volume.BlockSize <<= volume.BlockLogShift;
                    volume.BlockShift += volume.BlockLogShift;
                }
            }

            // check rootblock type
            if (!IsRootBlock(rbl))
            {
                AddError("not an AFS, PFS-II or PFS-III disk");
                if (ss.Info)
                {
                    return Error.None;
                }

                okuser = volume.AskUser("Rootblock not found.\n" +
                                        "Select ok to search for misplaced rootblock.", "ok", "cancel");

                if (okuser && (bloknr = await VolSearchFileSystem()) != 0)
                {
                    volume.ShowMsg("\nRootblock found. Repartitioning .. \n");
                    if ((error = await Repartition(bloknr)) != Error.None)
                        return error;

                    volume.AskUser(
                        "Partition is misplaced. Probable cause of this problem\n" +
                        "is the use of the 'Get Drive Definition' option in\n" +
                        "HDToolbox after the disk was formatted\n\n" +
                        "Now a mountfile will be needed to mount the partition.\n" +
                        "Press CONTINUE to select location to store the\n" +
                        "mountlist", "CONTINUE", null);

                    WriteMountFile();

                    volume.AskUser("Now starting to check/repair\n" +
                                   "relocated partition", "CONTINUE", null);

                    return await GetRootBlock();
                }

                okuser = volume.AskUser("Rootblock not found.\n" +
                                        "Select ok to build a new one.\n" +
                                        "WARNING: Make sure this is a PFS partition.\n" +
                                        "Non-PFS disks will be destroyed by this operation\n",
                    "ok", "cancel");

                if (okuser)
                    return await BuildRootBlockHub();

                aborting = true;
                return Error.Aborted;
            }

            error = await CheckRootBlock();

            if (ss.Info)
            {
                return Error.None;
            }

            switch (error)
            {
                case Error.None:
                    break;

                case Error.Repartition:
                    if ((error = await Repartition(volume.FirstBlock + DoctorConstants.ROOTBLOCK)) != Error.None)
                        return error;

                    volume.AskUser(
                        "The partition information stored in the RDB does\n" +
                        "not match the partition information used when the\n" +
                        "disk was formatted. Probable cause of this problem\n" +
                        "is the use of the 'Get Drive Definition' option in\n" +
                        "HDToolbox after the disk was formatted\n\n" +
                        "Now a mountfile will be needed to mount the partition.\n" +
                        "Press CONTINUE to select location to store the\n" +
                        "mountlist", "CONTINUE", null);

                    WriteMountFile();

                    volume.AskUser("Now starting to check/repair\n" +
                                   "redefined partition", "CONTINUE", null);

                    return await GetRootBlock();

                case Error.OptionsError:
                    return error;

                default:
                    return await BuildRootBlockHub();
            }

            ExitBlock();
            return Error.None;
        }

        private bool MakeMountFile(Stream stream)
        {
            try
            {
                using (var mf = new StreamWriter(stream, Encoding.ASCII))
                {
                    var dosenvec = volume.DosEnvec;
                    mf.NewLine = "\n";
                    mf.WriteLine("/****************************************/");
                    mf.WriteLine("/* PFSDoctor generated mountlist        */");
                    mf.WriteLine("/****************************************/");
                    mf.WriteLine("REPAIRED:");
                    mf.WriteLine($"\tDevice         = {volume.ExecDevice}");
                    mf.WriteLine($"\tUnit           = {volume.ExecUnit}");
                    mf.WriteLine("\tBuffers        = 300");
                    mf.WriteLine($"\tBufMemType     = {dosenvec.de_BufMemType}");
                    mf.WriteLine($"\tFlags          = {volume.Flags}");
                    mf.WriteLine($"\tDosType        = 0x{dosenvec.de_DosType:x}");
                    mf.WriteLine("\tGlobVec        = -1");
                    mf.WriteLine($"\tInterleave     = {dosenvec.de_Interleave}");
                    mf.WriteLine("\tReserved       = 2");
                    mf.WriteLine($"\tLowCyl         = {volume.FirstBlock}");
                    mf.WriteLine($"\tHighCyl        = {volume.LastBlock}");
                    mf.WriteLine("\tMount          = 1");
                    mf.WriteLine("\tSurfaces       = 1");
                    mf.WriteLine("\tBlocksPerTrack = 1");
                    mf.WriteLine("\tPriority       = 10");
                    mf.WriteLine("\tStackSize      = 600");
                    mf.WriteLine($"\tMaxTransfer    = {dosenvec.de_MaxTransfer}");
                    mf.WriteLine($"\tMask           = 0x{dosenvec.de_Mask:x}");
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private async Task<uint> VolSearchFileSystem()
        {
            return await SearchFileSystem(
                (int)((volume.FirstBlock > 1024) ? volume.FirstBlock - 1024 : 0),
                (int)(volume.FirstBlock + 1024));
        }

        private bool IsRootBlock(RootBlock r)
        {
            uint modemask;

            // check rootblock type
            if (r.DiskType != Constants.ID_PFS_DISK && r.DiskType != Constants.ID_PFS2_DISK)
            {
                if (ss.Verbose)
                    volume.ShowMsg($"Unexpected rootblock id 0x{r.DiskType:x8}\n");
                return false;
            }

            // check options
            // require non-null options to accept rootblock as such,
            // otherwise it could be a bootblock
            modemask = Constants.MODE_HARDDISK + Constants.MODE_SPLITTED_ANODES + Constants.MODE_DIR_EXTENSION;
            if ((r.Options & modemask) != modemask)
            {
                if (ss.Verbose)
                    volume.ShowMsg($"Unexpected rootblock options 0x{r.Options:x8}\n");
                return false;
            }

            return true;
        }

        /* check loaded rootblock
         */
        private async Task<Error> CheckRootBlock()
        {
            var error = Error.None;
            var dirty = false;
            uint modemask, resblocksize;

            modemask = Constants.MODE_HARDDISK + Constants.MODE_SPLITTED_ANODES + Constants.MODE_DIR_EXTENSION
                       + Constants.MODE_DELDIR + Constants.MODE_SIZEFIELD + Constants.MODE_EXTENSION +
                       Constants.MODE_DATESTAMP
                       + Constants.MODE_SUPERINDEX + Constants.MODE_SUPERDELDIR + Constants.MODE_EXTROVING +
                       Constants.MODE_LONGFN
                       + Constants.MODE_LARGEFILE + DoctorConstants.MODE_STORED_GEOM;

            if ((rbl.Options & ~modemask) != 0)
            {
                AddError("Unknown options enabled. Ask for an upgrade.");
                return Error.OptionsError;
            }

            // check the fields
            if (rbl.DiskNameLength == 0)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                    AddError("Volume has no name");
                else
                {
                    FixedError("Volume had no name");
                    rbl.DiskName = "doctor";
                    dirty = true;
                }
            }

            volume.DiskName = rbl.DiskName;
            volume.ShowMsg("Checking disk ");
            volume.ShowMsg(volume.DiskName);
            volume.ShowMsg("\n");
            volume.ShowMsg($"Option mask: {rbl.Options:x8}\n");
            volume.ShowMsg($"Reserved blocksize: {rbl.ReservedBlksize}\n");
            volume.ShowMsg($"Reserved block cluster: {rbl.RblkCluster}\n");

            resblocksize = 1024;
            if (rbl.DiskType == Constants.ID_PFS2_DISK)
            {
                if (volume.DiskSize > DoctorConstants.MAXDISKSIZE4K)
                {
                    AddError("Too large (>1.6TB) partition size");
                    return Error.OptionsError;
                }

                if (volume.DiskSize > DoctorConstants.MAXDISKSIZE1K)
                {
                    resblocksize = 2048;
                    if (volume.DiskSize > DoctorConstants.MAXDISKSIZE2K)
                        resblocksize = 4096;
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

            /* A reserved block is always at least one logical block: rescluster
             * would otherwise be 0 and every division by it trap. Only a corrupt
             * disktype puts a volume with >1024 byte logical blocks through the
             * branch above, which is exactly the case that has to be survived.
             */
            if (volume.BlockSize > resblocksize)
            {
                resblocksize = volume.BlockSize;
            }

            if (rbl.ReservedBlksize != resblocksize)
            {
                if (rbl.ReservedBlksize == 1024 || rbl.ReservedBlksize == 2048 || rbl.ReservedBlksize == 4096)
                {
                    AddError(
                        "Reserved blocksize is valid but does not match partition size. Driver size limit problem?");
                    return Error.OptionsError;
                }

                FixedError($"Wrong reserved blocksize of {rbl.ReservedBlksize}");
                rbl.ReservedBlksize = (ushort)resblocksize;
                dirty = true;
            }

            volume.ResCluster = resblocksize / volume.BlockSize;

            volume.ShowMsg($"First reserved block: {rbl.FirstReserved}\n");
            volume.ShowMsg($"Last reserved block: {rbl.LastReserved}\n");
            var blocks = (rbl.LastReserved - rbl.FirstReserved + 1) / volume.ResCluster;
            volume.ShowMsg($"Reserved blocks: {blocks}\n");
            volume.ShowMsg($"Free reserved blocks: {rbl.ReservedFree}\n");
            volume.ShowMsg($"Free blocks: {rbl.BlocksFree}\n");
            volume.ShowMsg($"Always free blocks: {rbl.AlwaysFree}\n");
            volume.ShowMsg($"Total blocks: {rbl.DiskSize}\n");

            // size must match, else repartition
            if ((rbl.Options & Constants.MODE_SIZEFIELD) != 0)
            {
                if (rbl.DiskSize != volume.FullDiskSizeNative)
                {
                    int difference;

                    /* uses rule: if difference less then 10%, assume mistake
                     * in RDB, otherwise assume mistake in rootblock
                     */
                    AddError("Wrong disksize");
                    volume.ShowMsg(
                        $"Rootblock size: {rbl.DiskSize}, driver reported size: {volume.FullDiskSizeNative}\n");
                    difference = Math.Abs((int)rbl.DiskSize - (int)volume.FullDiskSizeNative);
                    if (difference < volume.FullDiskSizeNative / 10)
                    {
                        error = Error.Repartition;
                    }
                    else if (mode != Mode.Check)
                    {
                        rbl.DiskSize = volume.FullDiskSizeNative;
                        dirty = true;
                        FixedError("set disksize");
                    }
                }
                else if (rbl.DiskSize != (volume.DiskSize << volume.BlockLogShift))
                {
                    /* Not an error: the partition is not a whole number of
                     * logical blocks, so the last partial block is unusable.
                     * volume.disksize counts the blocks between the aligned
                     * partition limits.
                     */
                    volume.ShowMsg(
                        $"Partition size is not logical block size aligned: {volume.DiskSize} usable blocks, driver reported {rbl.DiskSize} sectors\n");
                }
            }

            if (rbl.AlwaysFree < volume.DiskSize / 40 ||
                rbl.AlwaysFree > volume.DiskSize / 15)
            {
                FixedError("Allocation block buffer out of range");
                rbl.AlwaysFree = volume.DiskSize / 20;
                dirty = true;
            }

            if (dirty)
                return await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock, volume.BlockSize);

            return error;
        }

        /**************************************
         * GetRootBlockExtension
         **************************************/

        /* check rootblock extension. Do 'easy fix'. Returns
         * false if failure
         */
        private async Task<Error> GetRext()
        {
            Error error;
            uint bloknr, oldbloknr;

            bloknr = rbl.Extension;
            EnterBlock(bloknr);
            volume.ShowMsg("Checking rext\n");

            if (bloknr != 0)
            {
                if ((error = await VolGetBlock(rext, bloknr)) != Error.None)
                    return error;
            }

            if (bloknr == 0 || rext.ExtensionBlock.Id != Constants.EXTENSIONID)
            {
                AddError("Extension block has wrong id");
                if (mode == Mode.Repair)
                {
                    volume.Status(1, "Searching rext", 256);
                    oldbloknr = bloknr;
                    bloknr = await SearchBlock(Constants.EXTENSIONID, 0, bloknr, 0, 0, 0);
                    if (bloknr != 0)
                    {
                        FixedError($"Replaced block {oldbloknr} with backup {bloknr}");
                        rbl.Extension = bloknr;
                        return await GetRext();
                    }

                    volume.ShowMsg("No replacement block found\n");
                    if ((error = await BuildRext(rext)) != Error.None)
                        AddError("Building new rootblockextension failed");
                    else
                        FixedError("New rootblockextension created");
                    return error;
                }

                return Error.FatalError;
            }

            // ResBlockUsed(bloknr); in initreservedbitmap

            /* check if enabled */
            if ((rbl.Options & Constants.MODE_EXTENSION) == 0)
            {
                rbl.Options |= Constants.MODE_EXTENSION;
                await VolWriteBlock(rext);
                if (mode == Mode.Check || mode == Mode.Info)
                    AddError("MODE_EXTENSION is disabled");
                else
                    FixedError("MODE_EXTENSION was disabled");
            }

            /* fnsize */
            var rextData = rext.ExtensionBlock;
            if ((rbl.Options & Constants.MODE_LONGFN) != 0)
            {
                if (rextData.FnSize < 30 || rextData.FnSize > 108)
                {
                    var bericht = $"Illegal filename size of {rextData.FnSize}";
                    if (mode == Mode.Check || mode == Mode.Info)
                        AddError(bericht);
                    else
                    {
                        if (rextData.FnSize < 30)
                        {
                            FixedError("Teset filename size to 30");
                            rextData.FnSize = 30;
                        }
                        else
                        {
                            FixedError("Teset filename size to 100");
                            rextData.FnSize = 100;
                        }

                        await VolWriteBlock(rext);
                    }
                }

                volume.FnSize = rextData.FnSize - 1;
            }
            else
            {
                volume.FnSize = 32 - 1;
            }

            /* tbd fields */
            if (rextData.PostponedOp(0).Value != 0 ||
                rextData.PostponedOp(1).Value != 0 ||
                rextData.PostponedOp(2).Value != 0 ||
                rextData.PostponedOp(3).Value != 0)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                    AddError("Rootblock extension TBD not empty");
                else
                {
                    rextData.PostponedOp(0).Value =
                        rextData.PostponedOp(1).Value =
                            rextData.PostponedOp(2).Value =
                                rextData.PostponedOp(3).Value = 0;
                    await VolWriteBlock(rext);
                    FixedError("Rootblock extension TBD not empty");
                }
            }

            volume.ShowMsg($"Max file name length: {rextData.FnSize}\n");
            if ((rbl.Options & Constants.MODE_LARGEFILE) != 0)
            {
                volume.ShowMsg("Large file size support active\n");
            }

            ExitBlock();
            return Error.None;
        }

        /* check loaded rootblock extension
         */
        private bool GetPFS2Revision(out string vstring)
        {
            string buf;

            vstring = null;
            if (rext.Data == null)
                return false;

            var ver = (ushort)(rext.ExtensionBlock.Pfs2Version >> 16);
            var rev = (ushort)(rext.ExtensionBlock.Pfs2Version & 0xffff);

            if (ver < 16)
                buf = "AFS";
            else if (ver == 16)
                buf = rev < 22 ? "AFS" : "PFS-II";
            else if (ver >= 18)
                buf = "PFS-III";
            else
                buf = "PFS-II";

            vstring = $"{buf} {ver}.{rev}";

            return true;
        }

        /**************************************
         * RepairBootBlock
         **************************************/

        /* read and check bootblock (not store)
         */
        private async Task<Error> RepairBootBlock()
        {
            Error error;

            EnterBlock(DoctorConstants.BOOTBLOCK);

            // read bootblock
            var bootbl = new CachedBlock { Data = new byte[DoctorConstants.MAXRESBLOCKSIZE] };
            if ((error = await VolGetBlock(bootbl, DoctorConstants.BOOTBLOCK)) != Error.None)
            {
                AddError("Bootblock could not be loaded");
                return error;
            }

            // check bootblock type
            if (bootbl.BootBlock.DiskType != Constants.ID_PFS_DISK)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                    AddError("Bootblock has wrong ID");
                else
                {
                    bootbl.BootBlock.DiskType = Constants.ID_PFS_DISK;
                    FixedError("Bootblock had wrong ID");
                    if ((error = await VolWriteBlock(bootbl)) != Error.None)
                    {
                        AddError("Bootblock could not be written");
                        return error;
                    }
                }
            }

            ExitBlock();
            return Error.None;
        }

        /**************************************
         * RepairDeldir
         **************************************/

        /* Check plus fix deldir
         */
        private async Task<Error> RepairDeldir()
        {
            int i;
            bool remove;
            uint ddsize;
            bool rextdirty = false, rootdirty = false;

            volume.ShowMsg("Checking deldir\n");

            var rextData = rext.ExtensionBlock;

            /* check deldir version */
            if ((rbl.Options & Constants.MODE_DELDIR) != 0)
            {
                if ((rbl.Options & Constants.MODE_SUPERDELDIR) != 0)
                {
                    ddsize = rextData.DelDirSize;
                    volume.Status(0, "Deldir", ddsize);

                    for (i = 0; i < 32; i++)
                    {
                        if (rextData.DelDir(i).Value != 0)
                        {
                            EnterBlock(rextData.DelDir(i).Value);
                            if (ss.Verbose)
                            {
                                volume.ShowMsg($"DELDIR {i}\n");
                            }

                            remove = false;
                            if (i <= ddsize)
                            {
                                remove = true;
                                if (await DdCheckBlock(rextData.DelDir(i).Value, i))
                                {
                                    remove = ResBlockUsed(rextData.DelDir(i).Value) != Error.None;
                                }
                            }

                            if (remove && mode == Mode.Repair)
                            {
                                FixedError($"Deldir block {i} removed\n");
                                rextData.DelDir(i).Value = 0;
                                ddsize = Math.Min(ddsize, (uint)i + 1);
                                rextdirty = true;
                            }

                            ExitBlock();
                        }
                        else
                        {
                            ddsize = Math.Min(ddsize, (uint)i + 1);
                        }
                    }

                    if (ddsize != rextData.DelDirSize)
                    {
                        rextData.DelDirSize = (ushort)ddsize;
                        rextdirty = true;
                    }

                    if (rbl.DelDir != 0)
                    {
                        if (mode == Mode.Check || mode == Mode.Info)
                            AddError("Reference to old deldir");
                        else
                        {
                            rbl.DelDir = 0;
                            rootdirty = true;
                            FixedError("Reference to old deldir removed");
                        }
                    }
                }
                else /* old mode */
                {
                    if (rbl.DelDir == 0)
                    {
                        if (mode == Mode.Check || mode == Mode.Info)
                            AddError("Deldir enabled, but missing");
                        else
                        {
                            FixedError("Deldir enabled, but missing");
                            rbl.Options &= ~(uint)Constants.MODE_DELDIR;
                            rootdirty = true;
                        }
                    }
                    else
                    {
                        remove = false;
                        if (await DdCheckBlock(rbl.DelDir, 0))
                        {
                            if (ResBlockUsed(rbl.DelDir) != Error.None)
                                remove = true;
                        }
                        else
                            remove = true;

                        if (remove && mode == Mode.Repair)
                        {
                            FixedError("Deldir block removed\n");
                            rbl.DelDir = 0;
                            rbl.Options &= ~(uint)Constants.MODE_DELDIR;
                            rootdirty = true;
                        }
                    }
                }
            }
            else /* deldir disabled */
            {
                if (rextData.DelDirSize != 0)
                {
                    if (mode == Mode.Check || mode == Mode.Info)
                        AddError("Deldir size incorrect");
                    else
                    {
                        FixedError("Deldir size incorrect");
                        rextData.DelDirSize = 0;
                        rextdirty = true;
                    }
                }

                if (mode == Mode.Repair)
                {
                    for (i = 0; i < 32; i++)
                    {
                        if (rextData.DelDir(i).Value != 0)
                        {
                            FixedError("Deldir block removed");
                            rextData.DelDir(i).Value = 0;
                            rextdirty = true;
                        }
                    }
                }

                if (rbl.DelDir != 0)
                {
                    if (mode == Mode.Check || mode == Mode.Info)
                        AddError("Illegal deldir reference");
                    else
                    {
                        FixedError("Deldir block removed");
                        rbl.DelDir = 0;
                        rootdirty = true;
                    }
                }
            }

            if (rootdirty)
                await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock, volume.BlockSize);
            if (rextdirty)
                await VolWriteBlock(rext);

            return Error.None;
        }

        private async Task<bool> DdCheckBlock(uint bloknr, int seqnr)
        {
            Error error;
            int i;
            uint ddnr;
            uint blocks;
            uint guard;
            var delnode = new CAnode();
            uint size, high;

            volume.Progress(0, 1);

            /* get block */
            var ddblk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            if ((error = await VolGetBlock(ddblk, bloknr)) != Error.None)
            {
                AddError("Couldn't read deldirblock");
                return false;
            }

            /* check id */
            if (ddblk.Id != Constants.DELDIRID)
            {
                AddError("Deldirblock error");
                return false;
            }

            if (ddblk.DelDirBlock.SeqNr != seqnr)
            {
                AddError("Deldirblock error");
                return false;
            }

            // check entries
            for (i = 0; i < 31; i++)
            {
                blocks = 0;
                var dde = ddblk.DelDirBlock.Entries(i);
                if (dde.AnodeNr == 0)
                    continue;

                /* syntax fix */
                if (ss.Stage == Stage.Syntax)
                {
                    guard = MaxAnodeChain();
                    for (ddnr = dde.AnodeNr; ddnr != 0; ddnr = delnode.Next)
                    {
                        if (guard-- == 0 || !await GetAnode(delnode, ddnr, false) || IsAnodeUsed(ddnr))
                        {
                            if (mode == Mode.Check || mode == Mode.Info)
                                AddError("Delfile anode error");
                            else
                            {
                                FixedError("Delfile anode error");
                                dde.AnodeNr = 0;
                                await VolWriteBlock(ddblk);
                            }

                            break;
                        }

                        blocks += delnode.ClusterSize;
                    }

                    size = GetDDFileSize(dde, out high);
                    if (!FileSizeCheck(size, high, blocks))
                    {
                        var bericht = $"Delfile anode {dde.AnodeNr:x} error";
                        if (mode == Mode.Check || mode == Mode.Info)
                            AddError(bericht);
                        else
                        {
                            FixedError(bericht);
                            dde.AnodeNr = 0;
                            await VolWriteBlock(ddblk);
                        }
                    }
                }

                if (dde.AnodeNr == 0)
                    continue;

                guard = MaxAnodeChain();
                for (ddnr = dde.AnodeNr; ddnr != 0; ddnr = delnode.Next)
                {
                    /* a failed read leaves delnode holding the previous anode,
                     * so the same ddnr would be marked used over and over
                     */
                    if (guard-- == 0 || !await GetAnode(delnode, ddnr, false))
                        break;

                    /* mark anodes as used */
                    if (ss.Stage <= Stage.AnodeBitmap)
                        AnodeUsed(ddnr);

                    /* for dde only the anodes need to be checked */
                    // if (ss.stage == doubledel && isAnodeDouble(ddnr))
                    //	dde->anodenr = 0;
                }
            }

            return true;
        }

        /**************************************
         * Directory tree checker			L2
         **************************************/

        private DirStack currentobject;
        private string objectname = string.Empty;
        private const string dirtype = "DIR ";
        private const string filetype = "FILE ";
        private const string hardlinktype = "HARDLINK ";
        private const string softlinktype = "SOFTLINK ";
        private const string rollovertype = "ROLLOVER ";
        private const string unknowntype = "OBJECT ";

        private void PushDirStack(DirStack item)
        {
            item.Parent = currentobject;
            currentobject = item;
        }

        private void PopDirStack()
        {
            currentobject = currentobject.Parent;
        }

        private void GetObjectName()
        {
            DirStack n, ds;
            var b = new StringBuilder();

            objectname = string.Empty;
            if (currentobject == null)
                return;
            ds = null;
            while (ds != currentobject)
            {
                for (n = currentobject; n.Parent != ds; n = n.Parent)
                {
                }

                if (n.Parent != null && n.Parent.Parent != null)
                    b.Append("/");
                b.Append(n.Name);
                ds = n;
            }

            objectname = b.ToString();
        }

        private void FixedFileError(string errortxt)
        {
            if (currentobject != null)
            {
                GetObjectName();
                volume.ShowMsg(currentobject.ObjectType);
                volume.ShowMsg(objectname);
                volume.ShowMsg("\n");
            }

            if (mode == Mode.Check || mode == Mode.Info)
                AddError(errortxt);
            else
                FixedError(errortxt);
        }

        private void AddFileError(string errortxt)
        {
            if (currentobject != null)
            {
                GetObjectName();
                volume.ShowMsg(currentobject.ObjectType);
                volume.ShowMsg(objectname);
                volume.ShowMsg("\n");
            }

            AddError(errortxt);
        }

        /* Check directory tree. Track bitmap if needed.
         * Returns fatal error (0 = e_none. continue and errors fixed)
         */
        private async Task<Error> RepairDirTree()
        {
            var stackbottom = new DirStack
            {
                Parent = null,
                Name = "/",
                AnodeNr = Constants.ANODE_ROOTDIR,
                ObjectType = dirtype
            };
            Error error;

            volume.Status(0, "root", 256);
            volume.ShowMsg("Starting directory check\n");
            PushDirStack(stackbottom);
            error = await RepairDir(null, null);
            PopDirStack();
            return error;
        }

        private async Task<Error> RepairDir(DirEntry de, CachedBlock parent)
        {
            uint mainanodenr, anodenr, parentanodenr, guard;
            var dirnode = new CAnode();
            Error error;

            stats.NumDirs++;
            if (de != null)
            {
                volume.Status(0, currentobject.Name, 0);
                if (ss.Verbose)
                {
                    volume.ShowMsg("DIR ");
                    volume.ShowMsg(currentobject.Name);
                    volume.ShowMsg("\n");
                }

                /* repair linkchain, remove if necessary */
                await RepairLinkChain(de, parent);
            }

            parentanodenr = parent != null ? parent.DirBlock.AnodeNr : 0;
            mainanodenr = de != null ? de.Anode : Constants.ANODE_ROOTDIR;
            guard = MaxAnodeChain();
            for (anodenr = mainanodenr; anodenr != 0; anodenr = dirnode.Next)
            {
                /* AnodeUsed() below would report the revisit of a looping chain,
                 * but its result is deliberately not acted on here
                 */
                if (guard-- == 0)
                {
                    AddFileError("Directory anode chain does not end");
                    return Error.Remove;
                }

                /* get anode */
                if (!await GetAnode(dirnode, anodenr, true))
                    return Error.Remove;

                /* check anode */
                if (dirnode.ClusterSize != 1)
                {
                    FixedFileError("Directory anode has illegal clustersize");
                    dirnode.ClusterSize = 1;
                    await SaveAnode(dirnode, anodenr);
                }

                EnterBlock(dirnode.BlockNr);
                error = await RepairDirBlock(dirnode.BlockNr, mainanodenr, parentanodenr);
                ExitBlock();
                switch (error)
                {
                    case Error.Empty:

                        // BUG: an empty leading dirblock cannot be freed like
                        // that, the anodes have to moved!
                        //
                        // if (!dirnode.next)
                        // {
                        ResBlockUsed(dirnode.BlockNr);
                        AnodeUsed(anodenr);
                        error = Error.None;
                        break;
                    // }
                    //
                    // fixedfileerror("Empty directory block");

                    case Error.Remove:

                        if (mode == Mode.Repair)
                            FixedFileError("Removing corrupt directory block");
                        else
                            AddFileError("Corrupt directory block");

                        await DeleteAnode(mainanodenr, anodenr);
                        break;

                    case Error.None:

                        ResBlockUsed(dirnode.BlockNr);
                        AnodeUsed(anodenr);
                        break;

                    default:

                        KillReservedBitmap();
                        break;
                }

                if ((error != Error.None) || aborting)
                    return error;
            }

            return Error.None;
        }

        /* Is there a usable entry at 'de'? The entry chain is walked by adding
         * de->next, a byte straight off the disk, so every step has to be checked
         * against the end of the block: a corrupt chain used to walk off the end
         * of the block buffer and get parsed, repaired and written from there.
         * A zero 'next' is the end-of-chain marker and always 'fits'.
         */
        private bool DirEntryFits(DirEntry de, CachedBlock blk)
        {
            var p = de.Offset;
            var end = (int)SizeOfResBlock;

            if (p < DirBlock.EntriesOffset || p >= end)
                return false;
            if (de.Next == 0)
                return true;
            return (p + DirEntry.OffsetOfStartOfName <= end) &&
                   (p + de.Next <= end);
        }

        private async Task<Error> RepairDirBlock(uint bloknr, uint anodenr, uint parent)
        {
            DirEntry entry;
            Error error;

            /* get block */
            var dirblk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            if ((error = await VolGetBlock(dirblk, bloknr)) != Error.None)
            {
                return Error.Remove;
            }

            /* check dirblock */
            if (dirblk.Id != Constants.DBLKID || dirblk.DirBlock.AnodeNr != anodenr ||
                dirblk.DirBlock.Parent != parent)
            {
                return Error.Remove;
            }

            entry = dirblk.DirBlock.FirstEntry;

            while (DirEntryFits(entry, dirblk) && entry.Next != 0)
            {
                if (await RepairDirEntry(entry, dirblk) != Error.None)
                {
                    if (mode == Mode.Repair)
                    {
                        redosyntax = true;
                        FixedFileError("Removing corrupt directory entry");
                        await RemoveDirEntry(entry, dirblk);
                    }
                    else
                    {
                        AddFileError("Corrupt directory entry");
                        entry = entry.NextEntry();
                    }
                }
                else
                    entry = entry.NextEntry();
            }

            if (!DirEntryFits(entry, dirblk))
                AddFileError($"Directory entry chain runs past the end of block {bloknr}");

            /* has to be done after main check, because an empty block can
             * arise
             */
            error = Error.None;
            if (mode == Mode.Repair)
            {
                entry = dirblk.DirBlock.FirstEntry;
                if (entry.Next == 0)
                    error = Error.Empty;
            }

            return error;
        }

        private async Task<Error> RepairDirEntry(DirEntry de, CachedBlock dirblk)
        {
            var error = Error.None;

            volume.Progress(0, 1);
            if (de == null || de.Next == 0)
                return Error.Empty;

            if (ss.Stage == Stage.Syntax)
            {
                if ((de.Next & 1) != 0)
                {
                    AddFileError("Odd directory entry length");
                    return Error.DirentryError;
                }

                if (de.NLength + DirEntry.OffsetOfNLength > de.Next)
                {
                    AddFileError("Invalid filename");
                    return Error.DirentryError;
                }

                if (de.NLength > volume.FnSize)
                {
                    AddFileError("Filename too long");
                    return Error.DirentryError;
                }

                if (de.FileNoteLength + de.NLength + DirEntry.OffsetOfNLength > de.Next)
                {
                    AddFileError("Invalid filenote");
                    return Error.DirentryError;
                }
            }

            var dirstack = new DirStack
            {
                Name = de.Name,
                AnodeNr = de.Anode,
                ObjectType = unknowntype
            };
            PushDirStack(dirstack);

            switch ((int)de.Type)
            {
                case Constants.ST_USERDIR:

                    dirstack.ObjectType = dirtype;
                    error = await RepairDir(de, dirblk);
                    break;

                case Constants.ST_SOFTLINK:

                    dirstack.ObjectType = softlinktype;
                    error = await RepairSoftLink(de, dirblk);
                    break;

                case Constants.ST_LINKFILE:
                case Constants.ST_LINKDIR:

                    dirstack.ObjectType = hardlinktype;
                    error = await RepairHardLink(de, dirblk);
                    if (error == Error.Remove)
                        redosyntax = true;
                    break;

                case Constants.ST_ROLLOVERFILE:

                    dirstack.ObjectType = rollovertype;
                    error = await RepairRollover(de, dirblk);
                    break;

                case Constants.ST_FILE:

                    dirstack.ObjectType = filetype;
                    error = await RepairFile(de, dirblk);
                    break;

                // case Constants.ST_PIPEFILE:
                // case Constants.ST_ROOT:
                default:

                    AddFileError("Invalid filetype");
                    error = Error.DirentryError;
                    break;
            }

            PopDirStack();
            return error;
        }

        private async Task<Error> RepairFile(DirEntry de, CachedBlock parent)
        {
            uint blokken;
            var extra = new ExtraFields();
            var filenode = new CAnode();
            Error error;
            uint anodenr, bl, guard;
            uint size, high;

            stats.NumFiles++;
            if ((error = GetExtraFields(extra, de)) != Error.None)
                return error;

            /* check anode chain */
            if (ss.Stage == Stage.Syntax)
            {
                if (de.Type != Constants.ST_ROLLOVERFILE && (extra.VirtualSize != 0 || extra.RollPointer != 0))
                {
                    extra.VirtualSize = 0;
                    extra.RollPointer = 0;
                    await SetExtraFields(extra, de, parent);
                    FixedFileError("Dangling rollover");
                }

                blokken = 0;
                guard = MaxAnodeChain();
                for (anodenr = de.Anode; anodenr != 0; anodenr = filenode.Next)
                {
                    if (guard-- == 0)
                    {
                        AddFileError("File anode chain does not end");
                        return Error.Remove;
                    }

                    if (await CheckAnode(filenode, anodenr, true) != Error.None)
                        return Error.Remove;

                    blokken += filenode.ClusterSize;
                }

                size = GetDEFileSize(de, extra, out high);
                if (!FileSizeCheck(size, high, blokken))
                {
                    AddFileError($"Invalid filesize {high:x8}-{size:x8} {(int)blokken}");
                    return Error.Remove;
                }
            }

            if (extra.Link != 0)
                await RepairLinkChain(de, parent);

            /* bitmap gen */
            for (anodenr = de.Anode; anodenr != 0; anodenr = filenode.Next)
            {
                /* On failure filenode keeps the previous anode's contents, so
                 * ignoring the result marked the previous file's blocks used a
                 * second time and re-read the same anodenr forever. The bitmap
                 * being generated here is incomplete either way: invalidate it
                 * so it is regenerated, rather than writing one built from
                 * stale data (or dropping the entry over a transient failure).
                 */
                if (!await GetAnode(filenode, anodenr, true))
                {
                    InvalidBitmap(volume.MainBitmap);
                    InvalidBitmap(volume.AnodeBitmap);
                    return Error.None;
                }

                if ((error = AnodeUsed(anodenr)) != Error.None)
                    break;

                for (bl = filenode.BlockNr; bl < filenode.BlockNr + filenode.ClusterSize; bl++)
                    if ((error = MainBlockUsed(bl)) != Error.None)
                        break;
            }

            if (error != Error.None)
            {
                /* invalidate bitmaps to force generation in next stage. */
                InvalidBitmap(volume.MainBitmap);
                InvalidBitmap(volume.AnodeBitmap);
                return error;
            }

            return Error.None;
        }

        /* Repair hardlink. If a hardlink is removed that has consequences for
         * the linkchain and the linked to file.
         */
        private async Task<Error> RepairHardLink(DirEntry de, CachedBlock dirblok)
        {
            var extra = new ExtraFields();
            var linknode = new CAnode();
            Error error;

            stats.NumHardLink++;
            if (GetExtraFields(extra, de) != Error.None)
                return Error.Remove;

            if (extra.Link == 0)
            {
                AddFileError("Invalid hardlink");
                return Error.Remove;
            }

            /* get linknode */
            if (!await GetAnode(linknode, de.Anode, true))
                return Error.Remove;

            /* check linknode linkdir (anode.blocknr) reference (objectdir is
             * checked by CheckLinkChain) */
            if (linknode.BlockNr != dirblok.DirBlock.AnodeNr)
            {
                AddFileError("Invalid linknode directory reference");
                return Error.Remove;
            }

            /* Check object reference
             * Find anodenr in objectdir, with problem of possible
             * corruption. Loose directories are not detected.
             * It is not checked if the link itself is infact a link,
             * just that it exists.
             */
            switch (await SearchInDir(linknode.ClusterSize, extra.Link))
            {
                case 0:
                    AddFileError("Dangling hardlink");
                    return Error.Remove;

                case Constants.ST_FILE:
                case Constants.ST_ROLLOVERFILE:
                case Constants.ST_SOFTLINK:
                case Constants.ST_LINKFILE:
                    if (de.Type == Constants.ST_LINKDIR)
                    {
                        FixedFileError("Invalid hardlink");
                        de.Type = Constants.ST_LINKFILE;
                        await VolWriteBlock(dirblok);
                    }

                    break;

                default:
                    if (de.Type == Constants.ST_LINKFILE)
                    {
                        FixedFileError("Invalid hardlink");
                        de.Type = Constants.ST_LINKDIR;
                        await VolWriteBlock(dirblok);
                    }

                    break;
            }

            if ((error = AnodeUsed(de.Anode)) != Error.None)
                return error;

            return Error.None;
        }

        private async Task<Error> RepairSoftLink(DirEntry de, CachedBlock dirblok)
        {
            stats.NumSoftLink++;
            if (de.FSize > 108)
                return Error.InvalidSoftlink;

            return await RepairFile(de, dirblok);
        }

        private async Task<Error> RepairRollover(DirEntry de, CachedBlock dirblok)
        {
            var extra = new ExtraFields();
            Error error;

            stats.NumRollover++;
            if ((error = GetExtraFields(extra, de)) != Error.None)
                return error;

            if (extra.VirtualSize > de.FSize ||
                extra.RollPointer > de.FSize)
            {
                FixedFileError("Invalid rollover fields");
                extra.VirtualSize = 0;
                extra.RollPointer = 0;
                await SetExtraFields(extra, de, dirblok);
            }

            return await RepairFile(de, dirblok);
        }

        /*
         * Helper functions
         */

        /* Upper bound on the length of an anode chain: a chain cannot have more
         * links than there are anodes in the reserved area. Used to break out of
         * a chain that loops back on itself, which would otherwise be walked
         * forever - the chain walks have no CTRL-C check either.
         */
        private uint MaxAnodeChain()
        {
            uint blocks, perblock;

            if (rbl == null || volume.ResCluster == 0 || rbl.LastReserved < rbl.FirstReserved)
                return 1;

            perblock = AnodesPerBlock;
            if (perblock == 0)
                return 1;

            blocks = (rbl.LastReserved - rbl.FirstReserved + 1) / volume.ResCluster;
            if (blocks > DoctorConstants.MAX_ANODE_CHAIN / perblock)
                return DoctorConstants.MAX_ANODE_CHAIN;

            return blocks * perblock;
        }

        private async Task<Error> RemoveDirEntry(DirEntry de, CachedBlock dirblk)
        {
            int dest, source, end;
            int entrysize, movelen;

            end = (int)SizeOfResBlock;
            dest = de.Offset;
            entrysize = de.Next;
            source = dest + entrysize;

            /* the entry has to lie entirely inside the block */
            if (entrysize == 0 || dest < DirBlock.EntriesOffset || source > end)
                return Error.FatalError;

            /* Move only what follows the entry. The old length was the distance
             * from the entry to the end of the block, which read 'entrysize'
             * bytes of heap past the buffer and wrote them into the tail of the
             * block. Clear the bytes the move vacates, so the chain keeps
             * terminating on a zero 'next' instead of on whatever was there.
             */
            movelen = end - source;
            Array.Copy(dirblk.Data, source, dirblk.Data, dest, movelen);
            Array.Clear(dirblk.Data, dest + movelen, entrysize);
            await VolWriteBlock(dirblk);

            return Error.None;
        }

        /* remove node from anodechain
         */
        private async Task<Error> DeleteAnode(uint anodechain, uint node)
        {
            var prevNode = new CAnode();
            var nextNode = new CAnode();
            uint previous, guard;

            /* node is head */
            if (anodechain == node)
            {
                if (!await GetAnode(nextNode, node, false))
                    return Error.AnodeError;

                if (nextNode.Next == 0 || !await GetAnode(nextNode, nextNode.Next, false))
                    return Error.AnodeError;

                await SaveAnode(nextNode, node);
            }
            else
            {
                /* find previous */
                previous = anodechain;
                guard = MaxAnodeChain();
                while (true)
                {
                    /* a chain that does not contain 'node', or loops, used to
                     * be walked forever (a zero 'next' re-read anode 0)
                     */
                    if (previous == 0 || guard-- == 0)
                        return Error.AnodeError;

                    if (!await GetAnode(prevNode, previous, false))
                        return Error.AnodeError;

                    if (prevNode.Next == node)
                        break; // we found it

                    previous = prevNode.Next;
                }

                await GetAnode(nextNode, prevNode.Next, false);
                prevNode.Next = nextNode.Next;
                await SaveAnode(prevNode, prevNode.Nr);
            }

            return Error.None;
        }

        /* Get the directory entry extension fields
         */
        private Error GetExtraFields(ExtraFields extrafields, DirEntry de)
        {
            var extra = new ushort[ExtraFields.SizeOfInWords];
            var fields = (int)de.Next; // offset relative to de
            ushort flags, i;

            // extract extrafields from directoryentry
            fields -= 2;
            flags = de.ReadWord(fields);
            for (i = 0; i < ExtraFields.SizeOfInWords; i++, flags >>= 1)
            {
                if ((flags & 1) != 0)
                {
                    fields -= 2;
                    extra[i] = de.ReadWord(fields);
                }
                else
                {
                    extra[i] = 0;
                }
            }

            extrafields.FromWords(extra);

            // check flags field
            if (flags != 0)
                AddFileError("Unknown extrafield flags found (ignored)");

            // patch protection lower 8 bits
            extrafields.Prot |= de.Protection;
            return Error.None;
        }

        /* Set extrafields. Requirement is that the new directory entry fits.
         * To make sure it does, extrafields are removed only, not added.
         */
        private async Task<Error> SetExtraFields(ExtraFields extrafields, DirEntry from, CachedBlock dirblk)
        {
            // original code uses MAX_ENTRYSIZE, which is smaller than max size of a long filename entry
            var entrybuffer = new byte[512];
            int dest, start, end;
            int movelen;
            int diff;
            int fromNext = from.Next;

            var to = new DirEntry(entrybuffer, 0);
            Array.Copy(dirblk.Data, from.Offset, entrybuffer, 0, fromNext);
            AddExtraFields(to, extrafields);

            /* make room for new direntry */
            diff = to.Next - fromNext;
            dest = from.Offset + to.Next;
            start = from.Offset + fromNext;
            end = (int)SizeOfResBlock;
            movelen = (diff > 0) ? (end - dest) : (end - start);
            Array.Copy(dirblk.Data, start, dirblk.Data, dest, movelen);

            /* add new direntry */
            Array.Copy(entrybuffer, 0, dirblk.Data, from.Offset, to.Next);

            return await VolWriteBlock(dirblk);
        }

        private void AddExtraFields(DirEntry direntry, ExtraFields extra)
        {
            ushort offset;
            int dirext;
            var array = new ushort[16];
            ushort i, j = 0;
            ushort flags = 0, orvalue;

            /* patch protection lower 8 bits */
            extra.Prot &= 0xffffff00;
            var fields = extra.ToWords();
            offset = (ushort)((DoctorConstants.SIZEOF_DIRENTRY + direntry.NLength + direntry.FileNoteLength) &
                              0xfffe);
            dirext = (byte)offset; // offset relative to direntry

            orvalue = 1;
            /* fill packed field array */
            for (i = 0; i < ExtraFields.SizeOfInWords; i++)
            {
                if (fields[i] != 0)
                {
                    array[j++] = fields[i];
                    flags |= orvalue;
                }

                orvalue <<= 1;
            }

            /* add fields to direntry */
            i = j;
            while (i != 0)
            {
                direntry.WriteWord(dirext, array[--i]);
                dirext += 2;
            }

            direntry.WriteWord(dirext, flags);

            direntry.Next = (byte)(offset + 2 * j + 2);
        }

        /* Repair the hardlinks chain referring to a file
         */
        private async Task<Error> RepairLinkChain(DirEntry de, CachedBlock dirblk)
        {
            var extra = new ExtraFields();
            var linknode = new CAnode();
            Error error;
            uint parent, guard;

            /* check extra fields */
            if ((error = GetExtraFields(extra, de)) != Error.None)
                return error;

            /* check if object has a linkchain */
            if (extra.Link == 0)
                return Error.None;

            if (!await GetAnode(linknode, extra.Link, false))
                return Error.AnodeError;

            parent = dirblk.DirBlock.AnodeNr;
            guard = MaxAnodeChain();
            for (;;)
            {
                if (guard-- == 0)
                {
                    AddFileError("Hardlink chain does not end");
                    return Error.AnodeError;
                }

                // check objectdir (clustersize) against actual objectdir
                if (linknode.ClusterSize != parent)
                {
                    FixedFileError("Invalid hardlink");
                    linknode.ClusterSize = parent;
                    await SaveAnode(linknode, linknode.Nr);
                }

                // check linkdir left to HardLink::Check
                if (await SearchInDir(linknode.BlockNr, linknode.Nr) == 0)
                {
                    FixedFileError("Dangling hardlink");
                    if ((error = await DeleteAnode(extra.Link, linknode.Nr)) != Error.None
                        && (linknode.Nr == extra.Link))
                    {
                        extra.Link = 0;
                        await SetExtraFields(extra, de, dirblk);
                    }
                }

                if (linknode.Next == 0)
                    return Error.None;

                if (!await GetAnode(linknode, linknode.Next, false))
                    return Error.AnodeError;
            }
        }

        /* Get & check an anode
         */
        private async Task<Error> CheckAnode(CAnode anode, uint nr, bool fix)
        {
            /* if an anodeblock does not exist, it is considered empty */
            if (!await GetAnode(anode, nr, fix))
                return Error.Empty;

            if (anode.ClusterSize == 0 && anode.BlockNr == 0 && anode.Next == 0)
                return Error.Empty;

            return Error.None;
        }

        /* Search file by anodenr in a directory
         * return filetype or 0 for not found
         */
        private async Task<int> SearchInDir(uint diranodenr, uint target)
        {
            var anode = new CAnode();
            DirEntry de;
            var error = Error.None;
            uint guard;
            int ftype;

            /* Get directory and find object */
            var dirblk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            guard = MaxAnodeChain();
            while (error == Error.None && diranodenr != 0)
            {
                if (guard-- == 0 || !await GetAnode(anode, diranodenr, false))
                    break;

                if ((error = await VolGetBlock(dirblk, anode.BlockNr)) != Error.None || aborting)
                    break;

                de = dirblk.DirBlock.FirstEntry;
                while (DirEntryFits(de, dirblk) && de.Next != 0)
                {
                    if (de.Anode == target)
                    {
                        ftype = de.Type;
                        return ftype;
                    }

                    de = de.NextEntry();
                }

                diranodenr = anode.Next;
            }

            return 0;
        }

        /**************************************
         * Anode tree checker				L2
         **************************************/

        /* Check anode tree.
         * Returns fatal errors (e_none = continue)
         */
        private async Task<Error> RepairAnodeTree()
        {
            uint i;
            Error error;
            uint childBlkNr;

            volume.ShowMsg("Checking anodetree\n");
            if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
            {
                volume.Status(0, "Anodetree", 100);

                for (i = 0; i < Constants.MAXSUPER + 1; i++)
                {
                    if ((childBlkNr = rext.ExtensionBlock.SuperIndex(i).Value) != 0)
                    {
                        if ((error = await RepairSuperIndex(rext.ExtensionBlock.SuperIndex(i), i)) != Error.None ||
                            aborting)
                            return error;

                        if (rext.ExtensionBlock.SuperIndex(i).Value != childBlkNr)
                            await VolWriteBlock(rext);
                    }
                }
            }
            else
            {
                volume.Status(0, "Anodetree", 100);

                for (i = 0; i < Constants.MAXSMALLINDEXNR + 1; i++)
                {
                    if ((childBlkNr = rbl.IdxSmallIndexBlocks(i).Value) != 0)
                    {
                        if ((error = await RepairAnodeIndex(rbl.IdxSmallIndexBlocks(i), i)) != Error.None || aborting)
                            return error;

                        if (rbl.IdxSmallIndexBlocks(i).Value != childBlkNr)
                            await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                                volume.BlockSize);
                    }
                }
            }

            return Error.None;
        }

        private Task<Error> RepairSuperIndex(UInt32Pointer bloknr, uint seqnr)
        {
            return RepairIndexBlock(Constants.SBLKID, RepairAnodeIndex, bloknr, seqnr);
        }

        private Task<Error> RepairAnodeIndex(UInt32Pointer bloknr, uint seqnr)
        {
            volume.Progress(0, 0); // reset progress bar
            return RepairIndexBlock(Constants.IBLKID, RepairAnodeBlock, bloknr, seqnr);
        }

        /* missing: check of first, reserved anodes.
         * if blocknr is 0 --> search and build
         */
        private async Task<Error> RepairAnodeBlock(UInt32Pointer bloknr, uint seqnr)
        {
            var error = Error.None;
            uint oldbloknr;

            // get anodeblock
            var ablk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            if (bloknr.Value != 0)
            {
                if ((error = await VolGetBlock(ablk, bloknr.Value)) != Error.None)
                {
                    return error;
                }

                EnterBlock(bloknr.Value);
            }

            // check syntax block
            if (ss.Stage == Stage.Syntax)
            {
                if (ablk.Id != Constants.ABLKID || ablk.AnodeBlock.SeqNr != seqnr)
                {
                    if (bloknr.Value != 0)
                    {
                        AddError($"Anodeblock seqnr {seqnr} blocknr {bloknr.Value} has an incorrect id");
                    }

                    if (mode == Mode.Repair)
                    {
                        volume.Status(1, "Searching anode block", 256);
                        oldbloknr = bloknr.Value;
                        if ((bloknr.Value = await SearchBlock(Constants.ABLKID, seqnr, oldbloknr, 0, 0, 0)) != 0)
                        {
                            FixedError($"Replaced block {oldbloknr} with backup {bloknr.Value}");
                        }
                        else
                        {
                            /* remove corrupt block */
                            FixedError("No replacement found - removing block");
                            ExitBlock();
                            return Error.Remove;
                        }
                    }
                }
            }

            ResBlockUsed(bloknr.Value);
            ExitBlock();
            return error;
        }

        /**************************************
         * Bitmap tree checker				L2
         **************************************/

        /* Check bitmap tree.
         * Returns fatal errors (e_none = continue)
         */
        private async Task<Error> RepairBitmapTree()
        {
            Error error;
            uint i, num;
            uint blknr;

            volume.Status(0, "Bitmap", 100);
            volume.ShowMsg("Checking bitmap\n");

            num = ((volume.LastBlock - volume.FirstBlock - rbl.LastReserved - 1) + 31) / 32;
            num = (num + IndexPerBlock * IndexPerBlock - 1) / (IndexPerBlock * IndexPerBlock);
            for (i = 0;
                 i < (((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
                     ? (Constants.MAXBITMAPINDEX + 1)
                     : (Constants.MAXSMALLBITMAPINDEX + 1));
                 i++)
            {
                blknr = rbl.IdxLargeBitmapIndex(i).Value;
                if (i < num)
                {
                    /* force bitmapindexblock to be searched and created */
                    if (blknr == 0)
                        rbl.IdxLargeBitmapIndex(i).Value = rbl.LastReserved;

                    if ((error = await RepairBitmapIndex(rbl.IdxLargeBitmapIndex(i), i)) != Error.None || aborting)
                        return error;

                    if (rbl.IdxLargeBitmapIndex(i).Value != blknr)
                        await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock, volume.BlockSize);
                }
            }

            return Error.None;
        }

        /* change blocknr if necessary
         */
        private Task<Error> RepairBitmapIndex(UInt32Pointer bloknr, uint seqnr)
        {
            volume.Progress(0, 0); // reset progress bar
            return RepairIndexBlock(Constants.BMIBLKID, RepairBitmapBlock, bloknr, seqnr);
        }

        private async Task<Error> RepairBitmapBlock(UInt32Pointer bloknr, uint seqnr)
        {
            Error error;
            uint oldbloknr;

            // get anodeblock
            var bmblk = new CachedBlock { Data = new byte[SizeOfResBlock] };
            if (GetBuildBlock(Constants.BMBLKID, seqnr) != null)
                return Error.None;

            if (bloknr.Value != 0)
            {
                if ((error = await VolGetBlock(bmblk, bloknr.Value)) != Error.None)
                {
                    return error;
                }
            }

            EnterBlock(bloknr.Value);

            // check syntax block
            if (ss.Stage == Stage.Syntax)
            {
                if (bmblk.Id != Constants.BMBLKID || bmblk.BitmapBlock.SeqNr != seqnr)
                {
                    if (bloknr.Value != 0)
                    {
                        AddError($"Bitmapblock seqnr {seqnr} blocknr {bloknr.Value} has an incorrect id");
                    }

                    if (mode == Mode.Repair)
                    {
                        volume.Status(1, "Searching bitmap block", 256);
                        oldbloknr = bloknr.Value;
                        if ((bloknr.Value = await SearchBlock(Constants.BMBLKID, seqnr, oldbloknr, 0, 0, 0)) != 0)
                        {
                            FixedError($"Replaced block {oldbloknr} with backup {bloknr.Value}");
                        }
                        else
                        {
                            volume.ShowMsg("No replacement block found\n");
                            error = BuildBitmapBlock(bmblk, seqnr);
                            ExitBlock();
                            if (error != Error.None)
                                AddError("Building new bitmapblock failed");
                            else
                                FixedError("New bitmapblock created");
                            return error;
                        }
                    }
                }
            }

            ResBlockUsed(bloknr.Value);
            ExitBlock();
            return Error.None;
        }

        /**************************************
         * Check index blocks.
         * Returns fatal errors (e_none = continue)
         */
        private async Task<Error> RepairIndexBlock(ushort bloktype, Func<UInt32Pointer, uint, Task<Error>> repairchild,
            UInt32Pointer bloknr, uint seqnr)
        {
            var error = Error.None;
            var iblk = new CachedBlock();
            CachedBlock cblk;
            uint i;
            uint childBlkNr, oldbloknr;

            EnterBlock(bloknr.Value);

            if ((cblk = GetBuildBlock(bloktype, seqnr)) != null)
            {
                iblk.BlockNr = cblk.BlockNr;
                iblk.Mode = cblk.Mode;
                iblk.Data = cblk.Data;
            }

            if (iblk.Data == null)
            {
                iblk.Data = new byte[SizeOfResBlock];

                /* read or build indexblock
                 */
                if ((error = await VolGetBlock(iblk, bloknr.Value)) != Error.None)
                {
                    return error;
                }

                /* check id */
                if (ss.Stage == Stage.Syntax)
                {
                    if (iblk.Id != bloktype || iblk.IndexBlock.SeqNr != seqnr)
                    {
                        if (bloknr.Value != 0)
                        {
                            AddError(
                                $"Indexblock {seqnr} blocknr {bloknr.Value} type {bloktype:x} has incorrect id of {iblk.Id:x}, {iblk.IndexBlock.SeqNr}");
                        }

                        if (mode == Mode.Repair)
                        {
                            volume.Status(1, "Searching index block", 256);
                            oldbloknr = bloknr.Value;
                            if ((bloknr.Value = await SearchBlock(bloktype, seqnr, oldbloknr, 0, 0, 0)) != 0)
                            {
                                FixedError($"Replaced block {oldbloknr} with backup {bloknr.Value}");
                                return await RepairIndexBlock(bloktype, repairchild, bloknr, seqnr);
                            }

                            volume.ShowMsg("No replacement block found\n");
                            if ((error = await BuildIndexBlock(iblk, bloktype, seqnr)) != Error.None)
                            {
                                AddError("Building new indexblock failed");
                                ExitBlock();
                                return error;
                            }

                            FixedError("New indexblock created");
                        }
                        else
                        {
                            /* check mode failure */
                            ResBlockUsed(bloknr.Value);
                            return Error.None;
                        }
                    }
                }
            }

            for (i = 0; i < LongsPerBmb; i++)
            {
                var index = iblk.IndexBlock.Index(i);
                if ((childBlkNr = index.Value) != 0)
                {
                    volume.Progress(0, 1);
                    switch (await repairchild(index, seqnr * LongsPerBmb + i))
                    {
                        case Error.None:
                            break;

                        default:
                            /* kill block */
                            index.Value = 0;
                            break;
                    }

                    if (aborting)
                        return error;

                    /* save changes */
                    if (childBlkNr != index.Value && iblk.Mode == Mode.Check)
                        await VolWriteBlock(iblk);
                }
            }

            ExitBlock();
            if (iblk.Mode == Mode.Check)
            {
                /* not necessary (allowed) for generated blocks */
                ResBlockUsed(bloknr.Value);
            }

            return error;
        }

        /**************************************
         * Bitmap Generator					L3
         **************************************/

        // public data

        // enable flags

        // public functions

        private async Task<Error> InitReservedBitmap()
        {
            uint reservedsize, bloknr, cluster;

            KillReservedBitmap();
            if (rbl.FirstReserved != 2)
            {
                FixedError("Illegal firstreserved");
                rbl.FirstReserved = 2;
            }

            // reservedsize cannot be smaller then 0 and is not allowed to take more than
            // 1/8th of the partition.
            volume.LastReserved = rbl.LastReserved;
            reservedsize = rbl.LastReserved / volume.ResCluster - rbl.FirstReserved + 1;

            if (reservedsize == 0 || reservedsize > volume.DiskSize / 8)
            {
                AddError($"Illegal reserved area size of {reservedsize} blocks");

                if ((bloknr = await SearchLastReserved(volume)) == 0)
                    return Error.FatalError;

                FixedError(null);
                rbl.LastReserved = volume.LastReserved = bloknr;
            }

            if ((rext.ExtensionBlock.ReservedRoving + 31) / 32 > reservedsize)
            {
                rext.ExtensionBlock.ReservedRoving = 0;
                await VolWriteBlock(rext);
            }

            // check cluster size which is the rootblock together with the reserved bitmap.
            // There is one bit per reserved block in the reserved bitmap
            cluster = 1 + ((reservedsize > 125 * 32) ? (reservedsize - 125 * 32 + 256 * 32 - 1) / (256 * 32) : 0);
            // cluster is rounded up to closest reserved blocksize
            cluster = (uint)((((cluster + rbl.ReservedBlksize / 1024 - 1) & ~(rbl.ReservedBlksize / 1024 - 1)) *
                              1024) >> volume.BlockShift);
            if (rbl.RblkCluster != cluster)
            {
                rbl.RblkCluster = (ushort)cluster;
                if (mode == Mode.Check || mode == Mode.Info)
                    FixedError("Wrong rootblock cluster size");
            }

            volume.ResBitmap = BgInitBitmap(rbl.FirstReserved, rbl.LastReserved, volume.ResCluster);
            if (volume.ResBitmap == null)
                return Error.FatalError;

            /* mark rootblock cluster as used */
            for (bloknr = 2; bloknr < 2 + cluster; bloknr += volume.ResCluster)
                ResBlockUsed(bloknr);

            return Error.None;
        }

        /* assumes valid anodetree syntax
         */
        private async Task<Error> InitAnodeBitmap()
        {
            int i;
            uint s;
            Error error;

            KillAnodeBitmap();
            var iblk = new CachedBlock { Data = new byte[SizeOfResBlock] };

            /* determine number of anodebitmap blocks
             * If any anode or indexblocks are generated, the anodebitmap has to be
             * destroyed and regenerated.
             */
            if ((rbl.Options & Constants.MODE_SUPERINDEX) != 0)
            {
                for (s = Constants.MAXSUPER; rext.ExtensionBlock.SuperIndex(s).Value == 0 && s != 0; s--)
                {
                }

                if ((error = await GetResBlock(iblk, Constants.SBLKID, s, false)) != Error.None)
                {
                    return error;
                }

                // original code doesn't check lower bound of i
                for (i = (int)IndexPerBlock - 1; i >= 0 && iblk.IndexBlock.Index((uint)i).Value == 0; i--)
                {
                }
            }
            else
            {
                for (s = 0, i = Constants.MAXSMALLINDEXNR; rbl.IdxSmallIndexBlocks((uint)i).Value == 0 && i != 0; i--)
                {
                }
            }

            s = s * IndexPerBlock * IndexPerBlock * AnodesPerBlock;
            s += ((uint)++i * IndexPerBlock * AnodesPerBlock) - 1;
            volume.AnodeBitmap = BgInitBitmap(0, s, 1);
            for (i = 0; i < Constants.ANODE_ROOTDIR; i++)
                AnodeUsed((uint)i);

            return Error.None;
        }

        /* Initialise generation of main bitmap
         */
        private Error InitMainBitmap()
        {
            KillMainBitmap();
            volume.MainBitmap = BgInitBitmap(rbl.LastReserved + 1,
                volume.LastBlock - volume.FirstBlock, 1);

            if (volume.MainBitmap == null)
                return Error.FatalError;
            return Error.None;
        }

        private void KillAnodeBitmap()
        {
            volume.AnodeBitmap = BgKillBitmap(volume.AnodeBitmap);
        }

        private void KillMainBitmap()
        {
            volume.MainBitmap = BgKillBitmap(volume.MainBitmap);
        }

        private void KillReservedBitmap()
        {
            volume.ResBitmap = BgKillBitmap(volume.ResBitmap);
        }

        /// <summary>
        /// Invalidate bitmap (InvalidBitmap macro).
        /// </summary>
        private static void InvalidBitmap(Bitmap bitmap)
        {
            // original macro doesn't check for null
            if (bitmap != null)
                bitmap.Valid = false;
        }

        /// <summary>
        /// Is bitmap valid (IsBitmapValid macro).
        /// </summary>
        private static bool IsBitmapValid(Bitmap bitmap) => bitmap != null && bitmap.Valid;

        /* Compare generated reserved bitmap with the actual bitmap
         * and fix differences.
         * Pre:
         *	- syntax disk is correct
         *	- rootblock read
         *	- reserved bitmap generated AND valid
         */
        private async Task<Error> RepairReservedBitmap()
        {
            uint i, j, mask, blknr, blocksfree = 0;
            var dirty = false;
            Error error;
            uint nuba = 0, ubna = 0;

            volume.Status(0, "Validating reserved", volume.ResBitmap.LwSize / 0xff);

            /* first save current rootblock */
            if ((error = await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                    volume.BlockSize)) != Error.None)
                return error;

            /* now get reserved bitmap with rootblock in memory */
            var lrb = new RootBlock(new byte[volume.BlockSize * rbl.RblkCluster]);

            var t = 0;
            for (i = 0; i < rbl.RblkCluster; i++)
            {
                if ((error = await CGetBlock(lrb.Data, t, DoctorConstants.ROOTBLOCK + volume.FirstBlock + i,
                        volume.BlockSize)) != Error.None)
                {
                    AddError("Reserved bitmap could not be loaded");
                    return error;
                }

                t += (int)volume.BlockSize;
            }

            // bitmap = (bitmapblock_t *)(lrb + 1);
            var bitmap = new BitmapBlock(lrb.Data, DoctorConstants.SIZEOF_ROOTBLOCK);
            if (bitmap.Id != Constants.BMBLKID)
            {
                /* this is also done at BUILD ROOTBLOCK */
                if (mode == Mode.Check || mode == Mode.Info)
                    AddError("Reserved bitmap id is wrong");
                else
                    FixedError("Reserved bitmap id was wrong");

                Array.Clear(lrb.Data, DoctorConstants.SIZEOF_ROOTBLOCK,
                    lrb.Data.Length - DoctorConstants.SIZEOF_ROOTBLOCK);
                bitmap.Id = Constants.BMBLKID;
            }

            /* now we can check */
            for (i = 0; i < volume.ResBitmap.LwSize; i++)
            {
                if ((i & 0xff) == 0)
                {
                    if (aborting)
                        break;
                    volume.Progress(0, 1);
                }

                var bitmapField = bitmap.Bitmap(i);
                for (j = 0; j < 32; j++)
                {
                    mask = 0x80000000 >> (int)j;
                    blknr = (i * 32 + j) * volume.ResBitmap.Step + volume.ResBitmap.Start;

                    if (blknr <= volume.ResBitmap.Stop)
                    {
                        if ((volume.ResBitmap.Map[i] & mask) !=
                            (bitmapField.Value & mask))
                        {
                            if ((volume.ResBitmap.Map[i] & mask) != 0)
                            {
                                nuba++;
                                if (ss.Verbose)
                                    FixedError($"Reserved block {blknr} not used but allocated");
                                lrb.ReservedFree++;
                            }
                            else
                            {
                                ubna++;
                                if (ss.Verbose)
                                    FixedError($"Reserved block {blknr} used but not allocated");
                                lrb.ReservedFree--;
                            }

                            bitmapField.Value ^= mask;
                            dirty = true;
                        }

                        if ((bitmapField.Value & mask) != 0)
                            blocksfree++;
                    }
                }
            }

            if (nuba > 0)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                    FixedError($"{nuba} reserved blocks not used but allocated");
            }

            if (ubna > 0)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                    FixedError($"{ubna} reserved blocks used but not allocated");
            }

            // check blocks free
            if (lrb.ReservedFree != blocksfree && !aborting)
            {
                if (mode == Mode.Check || mode == Mode.Info)
                {
                    FixedError($"Wrong number of reserved blocks free ({lrb.ReservedFree} <> {blocksfree})");
                }

                lrb.ReservedFree = blocksfree;
                dirty = true;
            }

            error = Error.None;
            if (dirty && !aborting)
            {
                t = 0;
                for (i = 0; i < lrb.RblkCluster; i++)
                {
                    error = await CWriteBlock(lrb.Data, t, DoctorConstants.ROOTBLOCK + volume.FirstBlock + i,
                        volume.BlockSize);
                    t += (int)volume.BlockSize;
                }

                Array.Copy(lrb.Data, rbl.Data, Math.Min((int)SizeOfResBlock, lrb.Data.Length));
            }

            return error;
        }

        /* Compare generated bitmap with actual bitmap and fix found
         * differences. Pre conditions:
         * - syntax disk correct
         * - main bitmap generated and VALID
         * - rootblock and rext loaded
         */
        private async Task<Error> RepairMainBitmap()
        {
            uint k = 0, i, j, bmseqnr;
            uint mask, blknr, blocksfree = 0;
            Error error;
            var dirty = false;
            uint nuba = 0, ubna = 0;

            if (ss.Verbose)
                volume.ShowMsg("Checking main bitmap\n");

            var gbm = volume.MainBitmap;
            volume.Status(0, "Validating main", gbm.LwSize / IndexPerBlock);

            for (bmseqnr = 0; bmseqnr <= gbm.LwSize / IndexPerBlock; bmseqnr++)
            {
                volume.Progress(0, 1);

                // original code reuses block data, which overwrites data of a build bitmap block
                var bmb = new CachedBlock { Data = new byte[SizeOfResBlock] };
                if ((error = await GetResBlock(bmb, Constants.BMBLKID, bmseqnr, true)) != Error.None || aborting)
                {
                    if ((error = BuildBitmapBlock(bmb, bmseqnr)) != Error.None)
                        return error;
                }

                // check block
                for (i = 0; i < IndexPerBlock; i++, k++)
                {
                    var bitmapField = bmb.BitmapBlock.Bitmap(i);
                    for (j = 0; j < 32; j++)
                    {
                        mask = 0x80000000 >> (int)j;
                        blknr = k * 32 + j + gbm.Start;

                        if (blknr <= gbm.Stop)
                        {
                            if ((bitmapField.Value & mask) !=
                                (gbm.Map[k] & mask))
                            {
                                if ((gbm.Map[k] & mask) != 0)
                                {
                                    nuba++;
                                    // sprintf(bericht, "block %d not used but allocated", blknr);
                                    // if (ss.verbose)
                                    //	fixederror(bericht);
                                    rbl.BlocksFree++;
                                }
                                else
                                {
                                    ubna++;
                                    // sprintf(bericht, "block %d used but not allocated", blknr);
                                    // if (ss.verbose)
                                    //	fixederror(bericht);
                                    rbl.BlocksFree--;
                                }

                                bitmapField.Value ^= mask;
                                dirty = true;
                                if ((error = await VolWriteBlock(bmb)) != Error.None)
                                {
                                    return error;
                                }
                            }

                            if ((bitmapField.Value & mask) != 0)
                                blocksfree++;
                        }
                    }
                }
            }

            if (nuba > 0)
            {
                FixedError($"{nuba} data blocks not used but allocated");
            }

            if (ubna > 0)
            {
                FixedError($"{ubna} data blocks used but not allocated");
            }

            // check blocks free
            if (rbl.BlocksFree != blocksfree)
            {
                FixedError($"Wrong number of blocks free ({rbl.BlocksFree} <> {blocksfree})\n");
                rbl.BlocksFree = blocksfree;
                dirty = true;
            }

            error = Error.None;
            if (dirty)
                error = await CWriteBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock, volume.BlockSize);

            return error;
        }

        /* Compare anodes with generated anode bitmap. Fix found
         * differences. Pre conditions:
         * - syntax disk correct
         * - anode bitmap generated and VALID
         * - rootblock and rext loaded
         */
        private async Task<Error> RepairAnodeBitmap()
        {
            uint i, anodenr;
            bool anodeused;
            var anode = new CAnode();
            var gbm = volume.AnodeBitmap;
            Error error;
            uint nuba = 0, ubna = 0;
            var progress = (gbm.Stop - gbm.Start) / 70;

            // original code divides by zero for small anode bitmaps
            if (progress == 0)
                progress = 1;

            if (ss.Verbose)
                volume.ShowMsg("Checking anode bitmap\n");
            volume.Status(0, "Validating anodes", (gbm.Stop - gbm.Start) / 32);

            for (i = gbm.Start; i <= gbm.Stop; i++)
            {
                if ((i % progress) == 0)
                {
                    if (aborting)
                        break;
                    volume.Progress(0, 1);
                }

                anodeused = (gbm.Map[i / 32] & (0x80000000U >> (int)(i % 32))) == 0;
                anodenr = ((i / AnodesPerBlock) << 16) + i % AnodesPerBlock;

                error = await CheckAnode(anode, anodenr, false);

                if ((error == Error.Empty) == anodeused)
                {
                    if (anodeused)
                    {
                        if (ss.Verbose)
                        {
                            FixedError($"Anode {anodenr:x} used but not allocated");
                        }

                        ubna++;
                        if (mode == Mode.Repair)
                        {
                            anode.ClusterSize = 0;
                            anode.BlockNr = 0xffffffff;
                            anode.Next = 0;
                            await SaveAnode(anode, anodenr);
                        }
                    }
                    else
                    {
                        if (ss.Verbose)
                        {
                            FixedError($"Anode {anodenr:x} not used but allocated");
                        }

                        nuba++;
                        if (mode == Mode.Repair)
                        {
                            anode.ClusterSize =
                                anode.BlockNr =
                                    anode.Next = 0;
                            await SaveAnode(anode, anodenr);
                        }
                    }
                }

                // loop would never end if stop is uint.MaxValue
                if (i == uint.MaxValue)
                    break;
            }

            if (nuba > 0)
            {
                FixedError($"{nuba} anodes not used but allocated");
            }

            if (ubna > 0)
            {
                FixedError($"{ubna} anodes used but not allocated");
            }

            return Error.None;
        }

        private Error ResBlockUsed(uint bloknr)
        {
            var error = Error.None;

            if (ss.Stage <= Stage.ResBitmap)
            {
                error = BgItemUsed(volume.ResBitmap, bloknr);
                if (error == Error.DoubleAllocation)
                {
                    AddError($"Reserved block {bloknr} is double allocated");
                }
            }

            return error;
        }

        private Error MainBlockUsed(uint bloknr)
        {
            var error = Error.None;

            if (ss.Stage <= Stage.MainBitmap)
            {
                error = BgItemUsed(volume.MainBitmap, bloknr);
                if (error == Error.DoubleAllocation)
                {
                    AddFileError($"Block {bloknr} is double allocated");
                }
            }

            return error;
        }

        private Error AnodeUsed(uint nr)
        {
            var error = Error.None;

            if (ss.Stage <= Stage.AnodeBitmap)
            {
                nr = (nr >> 16) * AnodesPerBlock + (ushort)nr;
                error = BgItemUsed(volume.AnodeBitmap, nr);
                if (error == Error.DoubleAllocation)
                {
                    AddFileError($"Anode {nr:x} is double allocated");
                }
            }

            return error;
        }

        private bool IsAnodeUsed(uint nr)
        {
            if (nr == 0) return false;
            nr = (nr >> 16) * AnodesPerBlock + (ushort)nr;
            return BgIsItemUsed(volume.AnodeBitmap, nr);
        }

        /* private functions
         */

        /* initialise a bitmap for generation. The range is start to stop with
         * stepsize step
         */
        private static Bitmap BgInitBitmap(uint start, uint stop, uint step)
        {
            var mybitmap = new Bitmap
            {
                Start = start,
                Stop = stop,
                Step = step,
                LwSize = (((stop - start + step) / step) + 31) / 32
            };

            mybitmap.Map = new uint[mybitmap.LwSize];
            for (var i = 0; i < mybitmap.Map.Length; i++)
                mybitmap.Map[i] = 0xffffffff;
            mybitmap.Valid = true;
            return mybitmap;
        }

        /// <summary>
        /// Kill bitmap (bg_KillBitmap). Returns null, which is assigned to bitmap.
        /// </summary>
        private static Bitmap BgKillBitmap(Bitmap bm)
        {
            return null;
        }

        private static Error BgItemUsed(Bitmap bm, uint nr)
        {
            uint index, bit;

            if (bm == null)
                return Error.None;

            if (nr < bm.Start || nr > bm.Stop)
                return Error.OutsideBitmapError;

            nr -= bm.Start;
            nr /= bm.Step;
            index = nr / 32;
            bit = nr % 32;

            // check & set bit
            if ((bm.Map[index] & (0x80000000U >> (int)bit)) != 0)
            {
                bm.Map[index] ^= (0x80000000U >> (int)bit);
                return Error.None;
            }

            return Error.DoubleAllocation;
        }

        private static bool BgIsItemUsed(Bitmap bm, uint nr)
        {
            uint index, bit;

            // original code doesn't check for null bitmap or range
            if (bm == null || nr < bm.Start || nr > bm.Stop)
                return false;

            nr -= bm.Start;
            nr /= bm.Step;
            index = nr / 32;
            bit = nr % 32;

            if ((bm.Map[index] & (0x80000000U >> (int)bit)) != 0)
                return false;
            return true;
        }
    }
}
