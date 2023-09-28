namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Blocks;

    public class Pfs3Doctor
    {
        public static Volume OpenVolume(Stream stream, uint surfaces, uint blocksPerTrack, uint lowCyl, uint highCyl, uint sizeBlock)
        {
            var cylsectors = surfaces * blocksPerTrack;
            var volume = new Volume
            {
                firstblock = lowCyl * cylsectors,
                lastblock = (highCyl + 1) * cylsectors - 1,
                Stream = stream
            };
            var b = volume.blocksize = sizeBlock << 2;
            int i;
            for (i = -1; b != 0; i++)
            {
                b >>= 1;
            }
            volume.blockshift = (short)i;
            volume.rescluster = 0;
            volume.disksize = volume.lastblock - volume.firstblock + 1;
            volume.lastreserved = volume.disksize - 256;	/* temp value, calculated later */
            volume.PartitionOffset = volume.firstblock * volume.blocksize;
            
            volume.cache = Device.InitCache(volume, 64, 32);		/* make this configurable ? */

            return volume;
        }

        public static async Task Scan(Volume volume, bool repair, bool verbose = false)
        {
            // if (mode == repair)
            //     opties = SSF_FIX|SSF_ANALYSE|SSF_GEN_BMMASK;
            // else if (mode == unformat)
            //     opties = SSF_UNFORMAT|SSF_FIX|SSF_ANALYSE|SSF_GEN_BMMASK;
            // else
            //     opties = SSF_CHECK|SSF_ANALYSE|SSF_GEN_BMMASK;
		
            // if (verbose)
            //     opties |= SSF_VERBOSE;

            // opties = opties;
            //
            // flags = opties;
            // if (opties & (SSF_CHECK|SSF_FIX))
            //     flags |= SSF_GEN_BMMASK;   

            var ss = new ss();
            var mode = Mode.repair;
            // volume.showmsg("Initializing\n");
            // ss.flags = flags;
            // ss.stage = syntax;
            // ss.pass = stats.pass = 1;
            // ss.verbose = flags & SSF_VERBOSE;
            // ss.unformat = flags & SSF_UNFORMAT;
            
            InitFullScan(volume);
            
            /* unformat .. */
            if (ss.unformat)
            {
                mode = Mode.repair; 		// !! niet really correct !! fix this
                //volume.showmsg("Unformatting...\n");
                // if (!(rbl = (rootblock_t *)AllocBufMem (Constants.MAXRESBLOCKSIZE)))
                // {
                //     adderror("couldn't allocate memory for rootblock");
                //     return exitStandardScan(e_out_of_memory);
                // }
                // if ((error = BuildRootBlock_hub()) || aborting)
                //     return exitStandardScan(error);
            }
            
            /* read rootblock */
            //volume.status(0, "rootblock", 100);
            await GetRootBlock(volume);
            //     return exitStandardScan(error);
            //
            // volume.progress(0, 40);
            // if ((error = RepairBootBlock()) || aborting)
            //     return exitStandardScan(error);
            //
        }
        
        public static void InitFullScan(Volume volume)
        {
            volume.buildblocks = new LinkedList<cachedblock>();
        }
        
/* read from disk and check
 * Returns error (0 = ERROR_NONE = ok).
 */
public static async Task GetRootBlock(Volume volume)
{
	//error_t error = e_none;
	int bloknr;
	int okuser;
	//struct FileRequester *freq;
	string mfname;
	//[FNSIZE], *t;
	bool ok = false;
	var stats = new stats();

	stats.enterblock(stats, Constants.ROOTBLOCK);
	//volume.showmsg("Checking rootblock\n");
	// if (rbl)
	// 	FreeBufMem(rbl);

	// if (!(rbl = (rootblock_t *)AllocBufMem (MAXRESBLOCKSIZE)))
	// {
	// 	adderror("couldn't allocate memory for rootblock");
	// 	return e_out_of_memory;
	// }

	// read rootblock 
	var rblBytes = await Device.GetBlock(volume, Constants.ROOTBLOCK + volume.firstblock, volume.blocksize);
	var rootBlock = RootBlockReader.Parse(rblBytes);

// 	// check rootblock type
// 	if (!IsRootBlock(rbl))
// 	{
// 		adderror("not an AFS, PFS-II or PFS-III disk");
// 		okuser = volume.askuser("Rootblock not found.\n"
// 			"Select ok to search for misplaced rootblock.", "ok", "cancel");
//
// 		if (okuser && (bloknr = vol_SearchFileSystem()))
// 		{
// 			volume.showmsg("\nRootblock found. Repartitioning .. \n");
// 			if ((error = Repartition(bloknr)))
// 				return error;
//
// 			volume.askuser(
// 				"Partition is misplaced. Probable cause of this problem\n"
// 				"is the use of the 'Get Drive Definition' option in\n"
// 				"HDToolbox after the disk was formatted\n\n"
// 				"Now a mountfile will be needed to mount the partition.\n"
// 				"Press CONTINUE to select location to store the\n"
// 				"mountlist", "CONTINUE", NULL);
//
// 			freq = AllocAslRequestTags(ASL_FileRequest, ASLFR_SleepWindow, TRUE, 
// 				ASLFR_TitleText, "Select mountfile", ASLFR_InitialFile, "mountme",
// 				ASLFR_InitialDrawer, "df0:", ASLFR_DoSaveMode, TRUE, TAG_DONE);
//
// 			do
// 			{
// 				if (AslRequestTags(freq, ASLFR_Window, CheckRepairWnd, TAG_DONE))
// 				{
// 					t = stpcpy(mfname, freq->fr_Drawer);
// 					t = stpcpy(t, freq->fr_File);
// 					if ((ok = MakeMountFile(mfname)))
// 						volume.showmsg("Mountlist written\n");
// 				}
// 				else
// 				{
// 					volume.showmsg("no mountlist written\n");
// 				}
// 			} while (!ok);
//
// 			FreeAslRequest(freq);	
// 			volume.askuser("Now starting to check/repair\n"
// 				"relocated partition", "CONTINUE", NULL);
//
// 			return GetRootBlock();
// 		}
// 		else
// 		{
// 			okuser = volume.askuser("Rootblock not found.\n"
// 			"Select ok to build a new one.\n"
// 			"WARNING: Make sure this is a PFS partition.\n"
// 			"Non-PFS disks will be destroyed by this operation\n",
// 			"ok", "cancel");
//
// 			if (okuser)
// 				return BuildRootBlock_hub();
// 			else
// 			{
// 				aborting = 1;
// 				return e_aborted;
// 			}
// 		}
// 	}
// 	
// 	error = CheckRootBlock();
//
// 	switch (error)
// 	{
// 		case e_none:
// 			break;
//
// 		case e_repartition:
// 			if ((error = Repartition(volume.firstblock+ROOTBLOCK)))
// 				return error;
//
// 			volume.askuser(
// 				"The partition information stored in the RDB does\n"
// 				"not match the partition information used when the\n"
// 				"disk was formatted. Probable cause of this problem\n"
// 				"is the use of the 'Get Drive Definition' option in\n"
// 				"HDToolbox after the disk was formatted\n\n"
// 				"Now a mountfile will be needed to mount the partition.\n"
// 				"Press CONTINUE to select location to store the\n"
// 				"mountlist", "CONTINUE", NULL);
//
// 			freq = AllocAslRequestTags(ASL_FileRequest, ASLFR_SleepWindow, TRUE, 
// 				ASLFR_TitleText, "Select mountfile", ASLFR_InitialFile, "mountme",
// 				ASLFR_InitialDrawer, "df0:", ASLFR_DoSaveMode, TRUE, TAG_DONE);
//
// 			do
// 			{
// 				if (AslRequestTags(freq, ASLFR_Window, CheckRepairWnd, TAG_DONE))
// 				{
// 					t = stpcpy(mfname, freq->fr_Drawer);
// 					t = stpcpy(t, freq->fr_File);
// 					if ((ok = MakeMountFile(mfname)))
// 						volume.showmsg("Mountlist written\n");
// 				}
// 				else
// 				{
// 					volume.showmsg("no mountlist written\n");
// 				}
// 			} while (!ok);
//
// 			FreeAslRequest(freq);	
// 			volume.askuser("Now starting to check/repair\n"
// 				"redefined partition", "CONTINUE", NULL);
//
// 			return GetRootBlock();
//
// 		case e_options_error:
// 			return error;
//
// 		default:
// 			return BuildRootBlock_hub();
// 	}
//
// 	exitblock();
// 	return e_none;
}
        

public static bool IsRootBlock(RootBlock r)
{
	// check rootblock type
	if (r.DiskType != Constants.ID_PFS_DISK && r.DiskType != Constants.ID_PFS2_DISK)
	{
		// if (ss.verbose)
		// 	volume.showmsg($"Unexpected rootblock id 0x{r.DiskType:x8}\n", r.disktype);
		return false;
	}

	// check options
	// require non-null options to accept rootblock as such,
	// otherwise it could be a bootblock 
	var modemask = RootBlock.DiskOptionsEnum.MODE_HARDDISK | RootBlock.DiskOptionsEnum.MODE_SPLITTED_ANODES | RootBlock.DiskOptionsEnum.MODE_DIR_EXTENSION;
	if ((r.Options & modemask) != modemask)
	{
		// if (ss.verbose)
		// 	volume.showmsg("Unexpected rootblock options 0x%08lx\n", r->options);
		return false;
	}

	return true;
}

    }
}