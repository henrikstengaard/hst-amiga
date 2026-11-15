namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Blocks;
    using Salvage;

    /// <summary>
    /// Salvage of directories and files.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Object found by salvage.
        /// </summary>
        private class SalvageNode
        {
            public uint Block;
            public SfsObject Object;
            public SalvageEntry Entry;
            public List<SalvageNode> Children { get; } = new List<SalvageNode>();
            public bool IsDir => Object == null || Object.IsDir;
        }

        private bool salvaging;
        private SalvageResult salvageResult;

        /// <summary>
        /// Valid object containers found in volume.
        /// </summary>
        private Dictionary<uint, ObjectContainer> salvageContainers;

        /// <summary>
        /// Objects in valid object containers by object node of parent directory.
        /// </summary>
        private Dictionary<uint, List<(uint Block, SfsObject Object)>> salvageObjectsByParent;

        /// <summary>
        /// Object nodes of objects added to directory tree.
        /// </summary>
        private HashSet<uint> salvageReached;

        /// <summary>
        /// Salvage directories and files from a smart file system volume and re-create them in target. All blocks in
        /// the volume are scanned for object containers and extent B-tree leaf containers, so directories and files can
        /// be salvaged even when root blocks, root object container, object container chains, object node tree or
        /// extent B-tree are damaged. Directories are read from the root directory by following object container
        /// chains. If a chain is broken, all objects with the directory as parent are used. Object node tree is used,
        /// when it's valid, to skip stale objects left in freed object containers. Objects that can't be reached from
        /// the root directory are salvaged as orphaned objects in a lost directory. Protection bits, comments and dates
        /// are preserved, while soft links are skipped and reported. A pending transaction is replayed in memory before
        /// salvaging. The volume is never written to.
        /// </summary>
        /// <param name="volume">Volume to salvage, opened with <see cref="OpenVolume"/>.</param>
        /// <param name="target">Target to re-create salvaged directories and files in.</param>
        /// <param name="options">Salvage options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result of salvage.</returns>
        public static async Task<SalvageResult> Salvage(DoctorVolume volume, ISalvageTarget target,
            SalvageOptions options = null, CancellationToken cancellationToken = default)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));
            if (target == null) throw new ArgumentNullException(nameof(target));

            // salvage runs in check mode, so the volume is never written to
            var doctor = new SmartFileSystemDoctor(volume, Mode.Check, cancellationToken) { salvaging = true };
            return await doctor.RunSalvage(target, options ?? new SalvageOptions());
        }

        private void SalvageMessage(string message)
        {
            salvageResult.Messages.Add(message);
            ShowMsg(message);
        }

        private async Task<SalvageResult> RunSalvage(ISalvageTarget target, SalvageOptions options)
        {
            salvageResult = new SalvageResult();
            salvageContainers = new Dictionary<uint, ObjectContainer>();
            salvageObjectsByParent = new Dictionary<uint, List<(uint, SfsObject)>>();
            salvageReached = new HashSet<uint> { Constants.RootNode };
            scannedExtents = new Dictionary<uint, List<ExtentBNode>>();

            ShowMsg($"Salvaging smart file system volume, block size {volume.BlockSize}, blocks {volume.Blocks}");

            shiftsBlock32 = SmartFileSystemHelper.Log2(volume.BlockSize) - Constants.BlckFAccuracy;
            bitmapBlocks = Bitmap.CalculateBitmapBlocks(volume.Blocks, volume.BlockSize);

            // root block, pending transaction, object node tree and extent B-tree are used when available
            var lastBlock = volume.Blocks - 1;
            var first = await ReadBlock(0);
            var last = await ReadBlock(lastBlock);
            var rootData = GetRootBlockProblem(first, 0) == null ? first :
                GetRootBlockProblem(last, lastBlock) == null ? last : null;
            var hasNodeTree = false;
            if (rootData != null)
            {
                rootBlock = RootBlockParser.Parse(rootData);
                caseSensitive = rootBlock.CaseSensitive;
                ClaimRootStructures();
                await CheckTransaction();
                await ReadNodeTree();
                await ReadExtentTree();
                hasNodeTree = nodeTreeValid;
            }
            else
            {
                SalvageMessage("Root blocks are invalid, objects with root directory as parent are used for root directory");
            }

            await SalvageScanVolume();

            bool IsLive(uint block, SfsObject sfsObject) =>
                !hasNodeTree || !oldNodes.TryGetValue(sfsObject.ObjectNode, out var node) || node.Data == block;

            // root directory
            ObjectContainer rootContainerBlock = null;
            if (rootBlock != null &&
                salvageContainers.TryGetValue(rootBlock.RootObjectContainer, out rootContainerBlock))
            {
                var root = rootContainerBlock.Objects.FirstOrDefault();
                if (root == null || root.ObjectNode != Constants.RootNode || !root.IsDir)
                {
                    rootContainerBlock = null;
                }
            }

            salvageResult.RootBlockFound = rootContainerBlock != null;
            if (rootBlock != null && rootContainerBlock == null)
            {
                SalvageMessage("Root object container is invalid, objects with root directory as parent are used for root directory");
            }

            ShowMsg("Reading directory tree");
            var rootNode = new SalvageNode { Block = rootBlock?.RootObjectContainer ?? 0 };
            await SalvageLoadDirectory(rootNode, Constants.RootNode, rootContainerBlock?.Objects[0].FirstDirBlock,
                IsLive, 0);

            if (options.SalvageOrphans)
            {
                await SalvageLoadOrphans(rootNode, options, IsLive);
            }

            ShowMsg("Writing directories and files");
            await SalvageWriteChildren(target, new List<string>(), rootNode);
            await target.Flush(cancellationToken);

            SalvageMessage(
                $"Salvaged {salvageResult.Directories} directories and {salvageResult.Files} files ({salvageResult.Bytes} bytes)");

            return salvageResult;
        }

        /// <summary>
        /// Scan all blocks in volume for object containers and extent B-tree leaf containers.
        /// </summary>
        private async Task SalvageScanVolume()
        {
            ShowMsg("Scanning volume for object containers and extents");
            var rootObjectContainer = rootBlock?.RootObjectContainer ?? uint.MaxValue;
            await ScanBlocks((block, data) =>
            {
                var id = GetUInt32(data, 0);
                if (id == Constants.ObjectContainerId && IsValidBlock(data, block, id))
                {
                    var container = ParseObjectContainer(data, block, block == rootObjectContainer, out _);
                    salvageContainers[block] = container;
                    if (block == rootObjectContainer ||
                        (container.Parent == 0 && container.Objects.FirstOrDefault()?.ObjectNode == Constants.RootNode))
                    {
                        return;
                    }

                    if (!salvageObjectsByParent.TryGetValue(container.Parent, out var objects))
                    {
                        objects = new List<(uint, SfsObject)>();
                        salvageObjectsByParent[container.Parent] = objects;
                    }

                    objects.AddRange(container.Objects.Select(x => (block, x)));
                }
                else if (id == Constants.BNodeContainerId)
                {
                    AddScannedExtents(data, block, false);
                }
            });

            ShowMsg($"Found {salvageContainers.Count} object containers");
        }

        /// <summary>
        /// Load directory by following object container chain. If chain is broken or unknown, all live objects with
        /// directory as parent are added.
        /// </summary>
        private async Task SalvageLoadDirectory(SalvageNode node, uint dirNode, uint? firstDirBlock,
            Func<uint, SfsObject, bool> isLive, int depth)
        {
            var broken = !firstDirBlock.HasValue;
            var block = firstDirBlock ?? 0;
            var visited = new HashSet<uint>();
            while (block != 0)
            {
                if (!salvageContainers.TryGetValue(block, out var container) || container.Parent != dirNode ||
                    !visited.Add(block))
                {
                    broken = true;
                    break;
                }

                foreach (var sfsObject in container.Objects)
                {
                    await SalvageAddChild(node, block, sfsObject, isLive, depth);
                }

                block = container.Next;
            }

            if (!broken)
            {
                return;
            }

            if (firstDirBlock.HasValue)
            {
                SalvageMessage($"Directory '{node.Entry?.Name ?? "/"}' has broken object container chain, objects with directory as parent are used");
            }

            if (!salvageObjectsByParent.TryGetValue(dirNode, out var objects))
            {
                return;
            }

            foreach (var (objectBlock, sfsObject) in objects.ToList())
            {
                if (isLive(objectBlock, sfsObject))
                {
                    await SalvageAddChild(node, objectBlock, sfsObject, isLive, depth);
                }
            }
        }

        /// <summary>
        /// Add object to directory. If an object with same name exists, the newest object is kept.
        /// </summary>
        private async Task SalvageAddChild(SalvageNode node, uint block, SfsObject sfsObject,
            Func<uint, SfsObject, bool> isLive, int depth)
        {
            if (!salvageReached.Add(sfsObject.ObjectNode))
            {
                return;
            }

            var child = new SalvageNode
            {
                Block = block,
                Object = sfsObject,
                Entry = new SalvageEntry
                {
                    Name = SalvageName(sfsObject),
                    Type = sfsObject.IsDir ? EntryType.Dir : sfsObject.IsSoftLink ? EntryType.SoftLink : EntryType.File,
                    Size = sfsObject.IsFile ? sfsObject.Size : 0,
                    Protection = ProtectionHelper.ToAmigaDosProtection(sfsObject.Protection),
                    Date = sfsObject.DateModified,
                    Comment = sfsObject.Comment.Length > Constants.MaxCommentLength
                        ? sfsObject.Comment.Substring(0, Constants.MaxCommentLength)
                        : sfsObject.Comment
                }
            };

            if (sfsObject.IsDir)
            {
                if (depth < MaxDepth)
                {
                    await SalvageLoadDirectory(child, sfsObject.ObjectNode, sfsObject.FirstDirBlock, isLive, depth + 1);
                }
                else
                {
                    SalvageMessage($"Directory '{child.Entry.Name}' exceeds maximum depth {MaxDepth}, objects are skipped");
                }
            }

            var upperName = UpperName(child.Entry.Name);
            var existing = node.Children.FirstOrDefault(x => UpperName(x.Entry.Name) == upperName);
            if (existing != null)
            {
                if (existing.Entry.Date >= child.Entry.Date)
                {
                    SalvageMessage($"Object '{child.Entry.Name}' in block {block} has same name as newer object in block {existing.Block}, object is skipped");
                    salvageResult.Skipped++;
                    return;
                }

                SalvageMessage($"Object '{existing.Entry.Name}' in block {existing.Block} has same name as newer object in block {block}, object is skipped");
                salvageResult.Skipped++;
                node.Children.Remove(existing);
            }

            node.Children.Add(child);
        }

        private static string SalvageName(SfsObject sfsObject)
        {
            var name = sfsObject.Name.Replace(':', '_').Replace('/', '_');
            if (name.Length > Constants.MaxNameLength)
            {
                name = name.Substring(0, Constants.MaxNameLength);
            }

            return name.Length == 0 ? $"_{sfsObject.ObjectNode}" : name;
        }

        /// <summary>
        /// Load live objects not reached from root directory as orphans in lost directory. Orphans are grouped by
        /// object node of their parent directory in sub directories of lost directory.
        /// </summary>
        private async Task SalvageLoadOrphans(SalvageNode rootNode, SalvageOptions options,
            Func<uint, SfsObject, bool> isLive)
        {
            var lostNode = new SalvageNode
            {
                Entry = new SalvageEntry
                {
                    Name = options.LostDirectoryName,
                    Type = EntryType.Dir,
                    Date = DateTime.Now,
                    Comment = string.Empty
                }
            };

            // live directories by object node and parent object node, used to find top most unreached directory
            var dirs = new Dictionary<uint, (uint Parent, uint Block, SfsObject Object)>();
            foreach (var entry in salvageObjectsByParent)
            {
                foreach (var (block, sfsObject) in entry.Value)
                {
                    if (sfsObject.IsDir && isLive(block, sfsObject))
                    {
                        dirs[sfsObject.ObjectNode] = (entry.Key, block, sfsObject);
                    }
                }
            }

            var groups = new Dictionary<uint, SalvageNode>();
            foreach (var entry in salvageObjectsByParent.OrderBy(x => x.Key).ToList())
            {
                foreach (var (block, sfsObject) in entry.Value.ToList())
                {
                    if (salvageReached.Contains(sfsObject.ObjectNode) || !isLive(block, sfsObject))
                    {
                        continue;
                    }

                    // find top most unreached directory, so directory structure of orphans is kept
                    var top = (Parent: entry.Key, Block: block, Object: sfsObject);
                    var guard = new HashSet<uint> { sfsObject.ObjectNode };
                    while (dirs.TryGetValue(top.Parent, out var parent) && !salvageReached.Contains(top.Parent) &&
                           guard.Add(top.Parent))
                    {
                        top = parent;
                    }

                    if (!groups.TryGetValue(top.Parent, out var group))
                    {
                        group = new SalvageNode
                        {
                            Entry = new SalvageEntry
                            {
                                Name = $"Dir_{top.Parent}",
                                Type = EntryType.Dir,
                                Date = DateTime.Now,
                                Comment = string.Empty
                            }
                        };
                        groups[top.Parent] = group;
                        lostNode.Children.Add(group);
                    }

                    await SalvageAddChild(group, top.Block, top.Object, isLive, 1);
                }
            }

            if (groups.Count == 0)
            {
                return;
            }

            salvageResult.OrphanedDirectories = groups.Count;
            SalvageMessage($"Found orphaned objects in {groups.Count} directories, salvaged in '{options.LostDirectoryName}'");

            // lost directory gets a unique name, if root directory has an object with same name
            var lostName = lostNode.Entry.Name;
            var index = 1;
            while (rootNode.Children.Any(x => UpperName(x.Entry.Name) == UpperName(lostNode.Entry.Name)))
            {
                lostNode.Entry.Name = $"{lostName}.{index++}";
            }

            rootNode.Children.Add(lostNode);
        }

        private async Task SalvageWriteChildren(ISalvageTarget target, List<string> parentPath, SalvageNode node)
        {
            foreach (var child in node.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = string.Join("/", parentPath.Concat(new[] { child.Entry.Name }));

                try
                {
                    if (child.IsDir)
                    {
                        await target.CreateDirectory(parentPath, child.Entry, cancellationToken);
                        salvageResult.Directories++;
                        await SalvageWriteChildren(target, parentPath.Concat(new[] { child.Entry.Name }).ToList(),
                            child);
                        await target.SetDirectoryMetadata(parentPath, child.Entry, cancellationToken);
                    }
                    else if (child.Object.IsFile)
                    {
                        await SalvageWriteFile(target, parentPath, child, path);
                    }
                    else
                    {
                        SalvageMessage(child.Object.IsSoftLink
                            ? $"Soft link '{path}' is skipped"
                            : $"Object '{path}' with bits 0x{child.Object.Bits:x2} is not supported and skipped");
                        salvageResult.Skipped++;
                    }
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    SalvageMessage($"Failed to salvage '{path}': {e.Message}");
                    salvageResult.Errors++;
                }
            }
        }

        private async Task SalvageWriteFile(ISalvageTarget target, List<string> parentPath, SalvageNode file,
            string path)
        {
            var (extents, complete) = SalvageGetExtents(file.Object);
            var size = file.Object.Size;
            var length = Math.Min(size, extents.Sum(x => (long)x.Blocks) * volume.BlockSize);
            file.Entry.Size = length;

            using (var stream = new ExtentDataStream(volume.Stream, volume.PartitionStartOffset, volume.BlockSize,
                       extents, length))
            {
                await target.WriteFile(parentPath, file.Entry, stream, cancellationToken);
            }

            salvageResult.Files++;
            salvageResult.Bytes += length;

            if (!complete || length < size)
            {
                salvageResult.PartialFiles++;
                SalvageMessage($"File '{path}' is partially salvaged, {length} of {size} bytes");
            }
        }

        /// <summary>
        /// Get extents of file by following extent chain. Extents from extent B-tree are used first, then extents
        /// found by scan with previous matching extent chain.
        /// </summary>
        private (IList<(uint Start, uint Blocks)>, bool) SalvageGetExtents(SfsObject file)
        {
            var extents = new List<(uint, uint)>();
            var blocksNeeded = (file.Size + (long)volume.BlockSize - 1) / volume.BlockSize;
            long blocks = 0;
            var key = file.Data;
            var expectedPrev = file.ObjectNode | Constants.ExtentPrevObjectNodeFlag;
            var visited = new HashSet<uint>();
            while (key != 0 && blocks < blocksNeeded)
            {
                if (!visited.Add(key))
                {
                    return (extents, false);
                }

                if (!oldExtents.TryGetValue(key, out var extent) || extent.Prev != expectedPrev)
                {
                    var candidates = scannedExtents.TryGetValue(key, out var list)
                        ? list
                        : new List<ExtentBNode>();
                    extent = candidates.FirstOrDefault(x => x.Prev == expectedPrev) ??
                             (oldExtents.TryGetValue(key, out var treeExtent) ? treeExtent : null) ??
                             (candidates.Count == 1 ? candidates[0] : null);
                }

                if (extent == null || extent.Blocks == 0 || (ulong)key + extent.Blocks > volume.Blocks)
                {
                    return (extents, false);
                }

                extents.Add((key, extent.Blocks));
                blocks += extent.Blocks;
                expectedPrev = key;
                key = extent.Next;
            }

            return (extents, blocks >= blocksNeeded);
        }
    }
}
