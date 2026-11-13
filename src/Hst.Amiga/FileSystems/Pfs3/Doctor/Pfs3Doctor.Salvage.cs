namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Converters;
    using Salvage;
    using Constants = Hst.Amiga.FileSystems.Pfs3.Constants;

    /// <summary>
    /// Salvage of directories and files. Not part of original PFSDoctor.
    /// </summary>
    public partial class Pfs3Doctor
    {
        /// <summary>
        /// Maximum directory depth salvaged, guards against corrupt directory structures.
        /// </summary>
        private const int SalvageMaxDepth = 256;

        /// <summary>
        /// Maximum size of soft link data read to report soft link target.
        /// </summary>
        private const int SalvageMaxSoftLinkSize = 1024;

        /// <summary>
        /// Directory block found in reserved area.
        /// </summary>
        private class SalvageDirBlock
        {
            public uint BlockNr { get; set; }
            public uint Datestamp { get; set; }
            public uint AnodeNr { get; set; }
            public uint Parent { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Anode block found in reserved area.
        /// </summary>
        private class SalvageAnodeBlock
        {
            public uint BlockNr { get; set; }
            public uint Datestamp { get; set; }
            public byte[] Data { get; set; }
        }

        /// <summary>
        /// Directory entry read from directory block.
        /// </summary>
        private class SalvageNode
        {
            public SalvageEntry Entry { get; set; }
            public int Type { get; set; }
            public uint Anode { get; set; }
            public uint Link { get; set; }
            public uint Datestamp { get; set; }
            public List<SalvageNode> Children { get; } = new List<SalvageNode>();
        }

        private SalvageResult salvageResult;
        private bool salvageRootValid;
        private uint salvageRootDatestamp;
        private Dictionary<uint, SalvageAnodeBlock> salvageAnodeBlocks;
        private Dictionary<uint, SalvageDirBlock> salvageDirBlocks;
        private Dictionary<uint, List<SalvageDirBlock>> salvageDirBlocksByAnode;
        private HashSet<uint> salvageVisitedDirs;
        private Dictionary<uint, SalvageNode> salvageFilesByAnode;

        /// <summary>
        /// Salvage directories and files from a PFS3 volume and re-create them in target. The reserved area is
        /// scanned for anode and directory blocks, so directories and files can be salvaged even when the root
        /// block, index blocks or bitmaps are damaged. Directories are read from the root directory using the
        /// newest anode blocks found. Directory blocks that can't be reached from the root directory are salvaged
        /// as orphaned directories in a lost directory. Protection bits, comments and dates are preserved. Hard
        /// links to files are salvaged as copies of the linked file, while soft links and hard links to
        /// directories are skipped and reported. The volume is never written to.
        /// </summary>
        /// <param name="volume">Volume to salvage, opened with <see cref="OpenVolume(Stream, RigidDiskBlocks.PartitionBlock)"/>.</param>
        /// <param name="target">Target to re-create salvaged directories and files in.</param>
        /// <param name="options">Salvage options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of salvage.</returns>
        public static async Task<SalvageResult> Salvage(Volume volume, ISalvageTarget target,
            SalvageOptions options = null, CancellationToken cancellationToken = default)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (target == null) throw new ArgumentNullException(nameof(target));

            /* salvage never writes to the volume */
            volume.WriteRawBlocks = (data, blocks, blocknr) => DevWriteBlocksDummy(volume, data, blocks, blocknr);

            var doctor = new Pfs3Doctor(volume, Mode.Check, cancellationToken);
            await doctor.InitCache(volume.Cache.LineSize, volume.Cache.NoLines, volume.BlockSize);
            try
            {
                return await doctor.RunSalvage(target, options ?? new SalvageOptions());
            }
            finally
            {
                await doctor.FreeCache();
            }
        }

        private async Task<SalvageResult> RunSalvage(ISalvageTarget target, SalvageOptions options)
        {
            salvageResult = new SalvageResult();
            salvageAnodeBlocks = new Dictionary<uint, SalvageAnodeBlock>();
            salvageDirBlocks = new Dictionary<uint, SalvageDirBlock>();
            salvageDirBlocksByAnode = new Dictionary<uint, List<SalvageDirBlock>>();
            salvageVisitedDirs = new HashSet<uint>();
            salvageFilesByAnode = new Dictionary<uint, SalvageNode>();

            await SalvageReadRootBlock();
            await SalvageScanReservedArea();

            /* read directory tree from root directory */
            volume.ShowMsg("Reading directory tree\n");
            var root = new SalvageNode { Type = Constants.ST_USERDIR, Anode = Constants.ANODE_ROOTDIR };
            await SalvageLoadDirectory(root, Constants.ANODE_ROOTDIR, "/", 0);

            if (options.SalvageOrphans)
            {
                await SalvageLoadOrphans(root, options);
            }

            /* write directories and files to target */
            volume.ShowMsg("Writing directories and files\n");
            await SalvageWriteChildren(target, new List<string>(), root);
            await target.Flush();

            volume.ShowMsg(
                $"Salvaged {salvageResult.Directories} directories and {salvageResult.Files} files " +
                $"({salvageResult.Bytes} bytes)\n");

            return salvageResult;
        }

        private void SalvageMessage(string message)
        {
            salvageResult.Messages.Add(message);
            volume.ShowMsg($"{message}\n");
        }

        /// <summary>
        /// Read root block to get reserved block size and reserved area. If root block isn't valid, reserved
        /// block size is calculated from volume size and reserved area is searched for.
        /// </summary>
        private async Task SalvageReadRootBlock()
        {
            /* reserved block size used by driver for volume size */
            uint resblocksize = 1024;
            if (volume.DiskSize > DoctorConstants.MAXDISKSIZE1K)
                resblocksize = volume.DiskSize > DoctorConstants.MAXDISKSIZE2K ? 4096U : 2048U;
            if (volume.BlockSize > resblocksize)
                resblocksize = volume.BlockSize;

            rbl = new RootBlock(new byte[DoctorConstants.MAXRESBLOCKSIZE]);
            salvageRootValid = await CGetBlock(rbl.Data, DoctorConstants.ROOTBLOCK + volume.FirstBlock,
                volume.BlockSize) == Error.None && IsRootBlock(rbl);
            salvageResult.RootBlockFound = salvageRootValid;

            var reservedValid = false;
            if (salvageRootValid)
            {
                volume.DiskName = rbl.DiskName;
                salvageRootDatestamp = rbl.Datestamp;
                volume.ShowMsg($"Root block found, disk name '{volume.DiskName}'\n");

                var r = (uint)rbl.ReservedBlksize;
                if ((r == 1024 || r == 2048 || r == 4096) && r >= volume.BlockSize)
                    resblocksize = r;
                else
                    SalvageMessage($"Invalid reserved block size {r} in root block, using {resblocksize}");

                reservedValid = rbl.FirstReserved >= DoctorConstants.ROOTBLOCK &&
                                rbl.FirstReserved < rbl.LastReserved && rbl.LastReserved < volume.DiskSize;
                if (!reservedValid)
                    SalvageMessage("Invalid reserved area in root block, searching for reserved area");
            }
            else
            {
                SalvageMessage("Root block not found, searching for reserved area");
                Array.Clear(rbl.Data, 0, rbl.Data.Length);
            }

            rbl.ReservedBlksize = (ushort)resblocksize;
            volume.ResCluster = resblocksize / volume.BlockSize;

            if (!reservedValid)
            {
                rbl.FirstReserved = 2;
                volume.LastReserved = volume.DiskSize - 1;
                rbl.LastReserved = await SearchLastReserved(volume);
            }

            volume.LastReserved = rbl.LastReserved;
            volume.ShowMsg($"Reserved block size {resblocksize}, reserved area {rbl.FirstReserved} - " +
                           $"{rbl.LastReserved}\n");
        }

        /// <summary>
        /// Scan reserved area for anode and directory blocks. For each anode block sequence number the newest
        /// anode block is used. Anode blocks newer than the root block are only used, if no other anode block
        /// with the same sequence number exists, so blocks left from a previous format are not preferred.
        /// </summary>
        private async Task SalvageScanReservedArea()
        {
            var size = SizeOfResBlock;
            var data = new byte[size];
            var newerAnodeBlocks = new Dictionary<uint, SalvageAnodeBlock>();

            volume.ShowMsg("Scanning reserved area\n");
            volume.Status(0, "Scanning reserved area", (rbl.LastReserved - rbl.FirstReserved) / volume.ResCluster);
            for (var blockNr = rbl.FirstReserved; blockNr <= rbl.LastReserved; blockNr += volume.ResCluster)
            {
                cancellationToken.ThrowIfCancellationRequested();
                volume.Progress(0, 1);

                if (blockNr + volume.FirstBlock + volume.ResCluster - 1 > volume.LastBlock)
                    break;

                if (await CGetBlock(data, blockNr + volume.FirstBlock, size) != Error.None)
                    continue;

                switch (BigEndianConverter.ConvertBytesToUInt16(data))
                {
                    case Constants.ABLKID:
                        var anodeBlock = new AnodeBlock(data);
                        var seqNr = anodeBlock.SeqNr;
                        var found = new SalvageAnodeBlock
                        {
                            BlockNr = blockNr,
                            Datestamp = anodeBlock.Datestamp
                        };
                        var blocks = salvageRootValid && found.Datestamp > salvageRootDatestamp
                            ? newerAnodeBlocks
                            : salvageAnodeBlocks;
                        if (!blocks.TryGetValue(seqNr, out var existing) || found.Datestamp >= existing.Datestamp)
                        {
                            found.Data = (byte[])data.Clone();
                            blocks[seqNr] = found;
                        }

                        break;

                    case Constants.DBLKID:
                        var dirBlock = new DirBlock(data);
                        var salvageDirBlock = new SalvageDirBlock
                        {
                            BlockNr = blockNr,
                            Datestamp = dirBlock.Datestamp,
                            AnodeNr = dirBlock.AnodeNr,
                            Parent = dirBlock.Parent,
                            Data = (byte[])data.Clone()
                        };
                        salvageDirBlocks[blockNr] = salvageDirBlock;
                        if (!salvageDirBlocksByAnode.TryGetValue(salvageDirBlock.AnodeNr, out var list))
                        {
                            list = new List<SalvageDirBlock>();
                            salvageDirBlocksByAnode[salvageDirBlock.AnodeNr] = list;
                        }

                        list.Add(salvageDirBlock);
                        break;
                }
            }

            foreach (var newer in newerAnodeBlocks.Where(x => !salvageAnodeBlocks.ContainsKey(x.Key)))
            {
                salvageAnodeBlocks[newer.Key] = newer.Value;
            }

            volume.ShowMsg($"Found {salvageAnodeBlocks.Count} anode blocks and {salvageDirBlocks.Count} " +
                           "directory blocks\n");
        }

        /// <summary>
        /// Get anode from anode blocks found in reserved area.
        /// </summary>
        /// <returns>Found, cluster size, block number and next anode.</returns>
        private (bool, uint, uint, uint) SalvageGetAnode(uint anodenr)
        {
            var seqNr = anodenr >> 16;
            var offset = anodenr & 0xffff;

            if (offset >= AnodesPerBlock || !salvageAnodeBlocks.TryGetValue(seqNr, out var anodeBlock))
                return (false, 0, 0, 0);

            var node = new AnodeBlock(anodeBlock.Data).Nodes(offset);
            return (true, node.ClusterSize, node.BlockNr, node.Next);
        }

        /// <summary>
        /// Get directory blocks of a directory. Directory blocks are read by following the anode chain of the
        /// directory. If the anode chain is broken, all directory blocks found for the directory are used.
        /// </summary>
        private IList<SalvageDirBlock> SalvageGetDirectoryBlocks(uint diranodenr, string path)
        {
            var chainBlocks = new List<SalvageDirBlock>();
            var guard = volume.DiskSize;
            var chainValid = true;
            for (var anodenr = diranodenr; anodenr != 0;)
            {
                if (guard-- == 0)
                {
                    chainValid = false;
                    break;
                }

                var (found, _, blockNr, next) = SalvageGetAnode(anodenr);
                if (!found || !salvageDirBlocks.TryGetValue(blockNr, out var dirBlock) ||
                    dirBlock.AnodeNr != diranodenr || chainBlocks.Contains(dirBlock))
                {
                    chainValid = false;
                    break;
                }

                chainBlocks.Add(dirBlock);
                anodenr = next;
            }

            if (chainValid && chainBlocks.Count > 0)
                return chainBlocks;

            salvageDirBlocksByAnode.TryGetValue(diranodenr, out var foundBlocks);
            var blocks = chainBlocks.Union(foundBlocks ?? Enumerable.Empty<SalvageDirBlock>()).ToList();

            SalvageMessage(blocks.Count == 0
                ? $"Directory '{path}' has no directory blocks"
                : $"Directory '{path}' has broken anode chain, using {blocks.Count} directory blocks found");

            return blocks;
        }

        /// <summary>
        /// Load directory entries and subdirectories recursively.
        /// </summary>
        private async Task SalvageLoadDirectory(SalvageNode node, uint diranodenr, string path, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!salvageVisitedDirs.Add(diranodenr))
            {
                SalvageMessage($"Directory '{path}' is already salvaged, skipping loop in directory structure");
                return;
            }

            if (depth > SalvageMaxDepth)
            {
                SalvageMessage($"Directory '{path}' is too deep, skipping");
                return;
            }

            var entries = new Dictionary<string, SalvageNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var dirBlock in SalvageGetDirectoryBlocks(diranodenr, path))
            {
                foreach (var entry in SalvageReadDirEntries(dirBlock, path))
                {
                    if (entries.TryGetValue(entry.Entry.Name, out var existing))
                    {
                        if (entry.Datestamp <= existing.Datestamp)
                            continue;
                    }

                    entries[entry.Entry.Name] = entry;
                }
            }

            foreach (var entry in entries.Values.OrderBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                node.Children.Add(entry);

                switch (entry.Type)
                {
                    case Constants.ST_USERDIR:
                        await SalvageLoadDirectory(entry, entry.Anode, SalvageCombinePath(path, entry.Entry.Name),
                            depth + 1);
                        break;
                    case Constants.ST_FILE:
                    case Constants.ST_ROLLOVERFILE:
                        salvageFilesByAnode[entry.Anode] = entry;
                        break;
                }
            }
        }

        private static string SalvageCombinePath(string path, string name) =>
            path.EndsWith("/") ? string.Concat(path, name) : string.Concat(path, "/", name);

        /// <summary>
        /// Read directory entries from directory block. Entries with invalid structure are skipped.
        /// </summary>
        private IEnumerable<SalvageNode> SalvageReadDirEntries(SalvageDirBlock dirBlock, string path)
        {
            var data = dirBlock.Data;
            var end = data.Length;
            var de = new DirBlock(data).FirstEntry;

            while (de.Offset + DirEntry.OffsetOfStartOfName <= end && de.Next != 0)
            {
                int next = de.Next;
                if ((next & 1) != 0 || de.Offset + next > end || next < DirEntry.OffsetOfStartOfName)
                {
                    SalvageMessage($"Directory '{path}' has corrupt directory entry in block {dirBlock.BlockNr}");
                    yield break;
                }

                var entry = SalvageReadDirEntry(de, dirBlock, path);
                if (entry != null)
                    yield return entry;

                de = de.NextEntry();
            }
        }

        private SalvageNode SalvageReadDirEntry(DirEntry de, SalvageDirBlock dirBlock, string path)
        {
            int next = de.Next;
            int nameLength = de.NLength;

            /* name and filenote length byte must fit in entry */
            if (nameLength == 0 || DirEntry.OffsetOfStartOfName + nameLength + 1 > next)
            {
                SalvageMessage($"Directory '{path}' has entry with invalid name in block {dirBlock.BlockNr}");
                return null;
            }

            var name = SalvageMakeValidName(de.Name);
            if (name != de.Name)
            {
                SalvageMessage($"Entry '{SalvageCombinePath(path, name)}' has invalid characters in name, " +
                               "replaced with '_'");
            }

            var commentLength = (int)de.FileNoteLength;
            var commentOffset = DirEntry.OffsetOfStartOfName + nameLength + 1;
            if (commentOffset + commentLength > next)
            {
                commentLength = 0;
            }

            var comment = commentLength > 0
                ? AmigaTextHelper.GetString(de.Data, de.Offset + commentOffset, commentLength)
                : string.Empty;

            /* extra fields are stored backwards from end of entry, flags word last */
            var extraWords = new ushort[ExtraFields.SizeOfInWords];
            var fields = next - 2;
            var flags = de.ReadWord(fields);
            for (var i = 0; i < ExtraFields.SizeOfInWords; i++, flags >>= 1)
            {
                if ((flags & 1) == 0)
                    continue;

                fields -= 2;
                if (fields < commentOffset + commentLength)
                {
                    break;
                }

                extraWords[i] = de.ReadWord(fields);
            }

            var extra = new ExtraFields();
            extra.FromWords(extraWords);

            var type = (int)de.Type;
            var size = type == Constants.ST_USERDIR ? 0L : ((long)extra.FSizeX << 32) | de.FSize;

            return new SalvageNode
            {
                Type = type,
                Anode = de.Anode,
                Link = extra.Link,
                Datestamp = dirBlock.Datestamp,
                Entry = new SalvageEntry
                {
                    Name = name,
                    Type = type == Constants.ST_USERDIR ? EntryType.Dir : EntryType.File,
                    Size = size,
                    Protection = (extra.Prot & 0xffffff00) | de.Protection,
                    Date = SalvageConvertDate(de.CreationDay, de.CreationMinute, de.CreationTick),
                    Comment = comment
                }
            };
        }

        private static string SalvageMakeValidName(string name)
        {
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] < 32 || chars[i] == '/' || chars[i] == ':' || chars[i] == 127)
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static DateTime SalvageConvertDate(ushort days, ushort minutes, ushort ticks)
        {
            /* clamp corrupt values to the end of the day. Amiga dates are local time, so they are not shifted
             * when set as last write time on host files.
             */
            return DateTime.SpecifyKind(Amiga.DateHelper.ConvertToDate(days, Math.Min((int)minutes, 24 * 60 - 1),
                Math.Min((int)ticks, 60 * 50 - 1)), DateTimeKind.Local);
        }

        /// <summary>
        /// Load directories that can't be reached from root directory. Orphaned directories referenced by other
        /// orphaned directories are loaded as subdirectories, while the rest are added to the lost directory.
        /// </summary>
        private async Task SalvageLoadOrphans(SalvageNode root, SalvageOptions options)
        {
            var unreached = salvageDirBlocksByAnode.Keys.Where(x => !salvageVisitedDirs.Contains(x)).ToList();
            if (unreached.Count == 0)
                return;

            /* directories referenced by entries in other orphaned directories */
            var referenced = new HashSet<uint>();
            foreach (var dirBlock in unreached.SelectMany(x => salvageDirBlocksByAnode[x]))
            {
                var de = new DirBlock(dirBlock.Data).FirstEntry;
                while (de.Offset + DirEntry.OffsetOfStartOfName <= dirBlock.Data.Length && de.Next != 0 &&
                       (de.Next & 1) == 0 && de.Offset + de.Next <= dirBlock.Data.Length)
                {
                    if (de.Type == Constants.ST_USERDIR && de.Anode != dirBlock.AnodeNr)
                        referenced.Add(de.Anode);
                    de = de.NextEntry();
                }
            }

            var lostName = SalvageUniqueName(root, options.LostDirectoryName);
            var lost = new SalvageNode
            {
                Type = Constants.ST_USERDIR,
                Entry = new SalvageEntry
                {
                    Name = lostName,
                    Type = EntryType.Dir,
                    Date = DateTime.Now,
                    Comment = string.Empty
                }
            };

            var lostPath = SalvageCombinePath("/", lostName);

            /* orphans not referenced by other orphans first, then remaining orphans in loops */
            var roots = unreached.Where(x => !referenced.Contains(x)).Concat(unreached.Where(referenced.Contains));
            foreach (var anodenr in roots)
            {
                if (salvageVisitedDirs.Contains(anodenr))
                    continue;

                var name = $"Dir_{anodenr}";
                var orphan = new SalvageNode
                {
                    Type = Constants.ST_USERDIR,
                    Anode = anodenr,
                    Entry = new SalvageEntry
                    {
                        Name = name,
                        Type = EntryType.Dir,
                        Date = DateTime.Now,
                        Comment = string.Empty
                    }
                };

                await SalvageLoadDirectory(orphan, anodenr, SalvageCombinePath(lostPath, name), 1);

                if (orphan.Children.Count == 0)
                    continue;

                lost.Children.Add(orphan);
                salvageResult.OrphanedDirectories++;
            }

            if (lost.Children.Count == 0)
                return;

            SalvageMessage($"Found {lost.Children.Count} orphaned directories, salvaging to '{lostPath}'");
            root.Children.Add(lost);
        }

        private static string SalvageUniqueName(SalvageNode dir, string name)
        {
            var uniqueName = name;
            for (var i = 1; dir.Children.Any(x => x.Entry.Name.Equals(uniqueName, StringComparison.OrdinalIgnoreCase)); i++)
            {
                uniqueName = $"{name}.{i}";
            }

            return uniqueName;
        }

        /// <summary>
        /// Write directories and files in directory to target.
        /// </summary>
        private async Task SalvageWriteChildren(ISalvageTarget target, List<string> path, SalvageNode dir)
        {
            foreach (var node in dir.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entryPath = "/" + string.Join("/", path.Concat(new[] { node.Entry.Name }));
                try
                {
                    switch (node.Type)
                    {
                        case Constants.ST_USERDIR:
                            await SalvageWriteDirectory(target, path, node, entryPath);
                            break;

                        case Constants.ST_FILE:
                        case Constants.ST_ROLLOVERFILE:
                            await SalvageWriteFile(target, path, node.Entry, node.Anode, entryPath);
                            break;

                        case Constants.ST_LINKFILE:
                            if (salvageFilesByAnode.TryGetValue(node.Link, out var linked))
                            {
                                var entry = new SalvageEntry
                                {
                                    Name = node.Entry.Name,
                                    Type = EntryType.File,
                                    Size = linked.Entry.Size,
                                    Protection = node.Entry.Protection,
                                    Date = node.Entry.Date,
                                    Comment = node.Entry.Comment
                                };
                                await SalvageWriteFile(target, path, entry, linked.Anode, entryPath);
                            }
                            else
                            {
                                salvageResult.Skipped++;
                                SalvageMessage($"Skipped hard link '{entryPath}', linked file not found");
                            }

                            break;

                        case Constants.ST_LINKDIR:
                            salvageResult.Skipped++;
                            SalvageMessage($"Skipped hard link to directory '{entryPath}'");
                            break;

                        case Constants.ST_SOFTLINK:
                            salvageResult.Skipped++;
                            SalvageMessage($"Skipped soft link '{entryPath}' to '{await SalvageReadSoftLink(node)}'");
                            break;

                        default:
                            salvageResult.Skipped++;
                            SalvageMessage($"Skipped entry '{entryPath}' with unknown type {node.Type}");
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    salvageResult.Errors++;
                    SalvageMessage($"Failed to salvage '{entryPath}': {e.Message}");
                }
            }
        }

        private async Task SalvageWriteDirectory(ISalvageTarget target, List<string> path, SalvageNode node,
            string entryPath)
        {
            volume.ShowMsg($"DIR {entryPath}\n");
            await target.CreateDirectory(path, node.Entry);
            salvageResult.Directories++;

            path.Add(node.Entry.Name);
            try
            {
                await SalvageWriteChildren(target, path, node);
            }
            finally
            {
                path.RemoveAt(path.Count - 1);
            }

            await target.SetDirectoryMetadata(path, node.Entry);
        }

        private async Task SalvageWriteFile(ISalvageTarget target, List<string> path, SalvageEntry entry,
            uint anodenr, string entryPath)
        {
            volume.ShowMsg($"FILE {entryPath}\n");
            using (var stream = new AnodeChainStream(volume, SalvageGetAnode, anodenr, entry.Size))
            {
                await target.WriteFile(path, entry, stream);

                salvageResult.Files++;
                salvageResult.Bytes += stream.Position;

                if (!stream.Complete)
                {
                    salvageResult.PartialFiles++;
                    SalvageMessage($"File '{entryPath}' partially salvaged, {stream.Position} of {entry.Size} " +
                                   $"bytes: {stream.FailureReason ?? "data not read"}");
                }
            }
        }

        private async Task<string> SalvageReadSoftLink(SalvageNode node)
        {
            var size = (int)Math.Min(node.Entry.Size, SalvageMaxSoftLinkSize);
            var data = new byte[size];
            using (var stream = new AnodeChainStream(volume, SalvageGetAnode, node.Anode, size))
            {
                var bytesRead = await stream.ReadAsync(data, 0, size, cancellationToken);
                var linkPath = AmigaTextHelper.GetString(data, 0, bytesRead);
                var nullIndex = linkPath.IndexOf('\0');
                return nullIndex >= 0 ? linkPath.Substring(0, nullIndex) : linkPath;
            }
        }
    }
}
