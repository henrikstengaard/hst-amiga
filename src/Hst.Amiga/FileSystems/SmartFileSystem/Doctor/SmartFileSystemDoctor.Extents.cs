namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Extent B-tree and file data.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Extents read from valid leaf B-tree node containers.
        /// </summary>
        private readonly Dictionary<uint, ExtentBNode> oldExtents;

        /// <summary>
        /// Valid B-tree node containers in extent B-tree.
        /// </summary>
        private readonly List<BNodeContainer> bNodeContainers;

        /// <summary>
        /// Blocks referenced by extent B-tree except root B-tree node container, valid or not. Blocks are reused
        /// when extent B-tree is rebuilt.
        /// </summary>
        private readonly List<uint> extentTreeBlocks = new List<uint>();

        /// <summary>
        /// Extent B-tree structure is valid.
        /// </summary>
        private bool extentTreeValid = true;

        /// <summary>
        /// Extents in leaf B-tree node containers found in admin spaces, used to find extents of files when extent
        /// B-tree is invalid.
        /// </summary>
        private Dictionary<uint, List<ExtentBNode>> scannedExtents;

        /// <summary>
        /// Files in directory tree.
        /// </summary>
        private readonly List<ObjModel> files;

        /// <summary>
        /// Keys of extents removed by repair.
        /// </summary>
        private readonly HashSet<uint> droppedExtents;

        /// <summary>
        /// Keys of extents changed by repair.
        /// </summary>
        private readonly HashSet<uint> changedExtents;

        private int LeafBNodeCapacity => (volume.BlockSize - Constants.BNodeContainerHeaderSize) / Constants.ExtentBNodeSize;
        private int InternalBNodeCapacity => (volume.BlockSize - Constants.BNodeContainerHeaderSize) / Constants.BNodeSize;

        /// <summary>
        /// Read extent B-tree.
        /// </summary>
        private async Task ReadExtentTree()
        {
            ShowMsg("Checking extent B-tree");

            // root B-tree node container is claimed with other structures with fixed location
            var root = rootBlock.ExtentBNodeRoot;
            await ReadBNodeContainer(root, 0, uint.MaxValue + 1UL, true, 0);

            if (extentTreeValid)
            {
                return;
            }

            // find extents in leaf containers not read from extent B-tree, so files can be read when extent B-tree is
            // broken. Leaf containers with checksum errors are used, as extents are only used when previous matches
            // extent chain of file.
            scannedExtents = new Dictionary<uint, List<ExtentBNode>>();
            var validBlocks = new HashSet<uint>(bNodeContainers.Select(x => x.OwnBlock));
            var candidates = AdminSpaces
                .SelectMany(x => Enumerable.Range(0, Constants.BlocksAdmin).Select(i => x.Space + (uint)i))
                .Concat(extentTreeBlocks).Concat(new[] { root })
                .Where(x => !validBlocks.Contains(x) && InVolume(x))
                .Distinct().OrderBy(x => x).ToList();
            foreach (var block in candidates)
            {
                AddScannedExtents(await ReadBlock(block), block, false);
            }
        }

        /// <summary>
        /// Add extents from leaf B-tree node container to scanned extents.
        /// </summary>
        /// <param name="data">Block data.</param>
        /// <param name="block">Block number.</param>
        /// <param name="requireChecksum">Only use leaf container, if checksum is valid.</param>
        private void AddScannedExtents(byte[] data, uint block, bool requireChecksum)
        {
            if (data == null || GetUInt32(data, 0) != Constants.BNodeContainerId || GetUInt32(data, 8) != block ||
                data[0xe] == 0 || data[0xf] != Constants.ExtentBNodeSize ||
                (requireChecksum && !ChecksumHelper.IsValid(data)))
            {
                return;
            }

            BNodeContainer container;
            try
            {
                container = BNodeContainerParser.Parse(data);
            }
            catch (IOException)
            {
                return;
            }

            foreach (var node in container.Nodes.Cast<ExtentBNode>())
            {
                if (!scannedExtents.TryGetValue(node.Key, out var list))
                {
                    list = new List<ExtentBNode>();
                    scannedExtents[node.Key] = list;
                }

                list.Add(node);
            }
        }

        /// <summary>
        /// Read B-tree node container and its children.
        /// </summary>
        /// <returns>False, if B-tree node container is invalid and can't be read.</returns>
        private async Task<bool> ReadBNodeContainer(uint block, ulong minKey, ulong maxKey, bool isRoot, int depth)
        {
            var data = await ReadBlock(block);
            var problem = GetBlockProblem(data, block, Constants.BNodeContainerId);
            BNodeContainer container = null;
            if (problem == null)
            {
                try
                {
                    container = BNodeContainerParser.Parse(data);
                }
                catch (IOException)
                {
                    problem = "node count or node size is invalid";
                }
            }

            if (container != null)
            {
                if (container.NodeSize != (container.IsLeaf ? Constants.ExtentBNodeSize : Constants.BNodeSize))
                {
                    problem = $"node size {container.NodeSize} is invalid";
                }
                else if (!isRoot && container.Nodes.Count == 0)
                {
                    problem = "B-tree node container is empty";
                }
                else if (depth > 32)
                {
                    problem = "extent B-tree is too deep";
                }
            }

            if (problem != null)
            {
                extentTreeValid = false;
                AddIssue(IssueType.BNodeContainerInvalid, block, null,
                    $"B-tree node container is invalid: {problem}, extent B-tree is rebuilt");
                return false;
            }

            bNodeContainers.Add(container);

            for (var i = 0; i < container.Nodes.Count; i++)
            {
                var node = container.Nodes[i];
                if (i > 0 && node.Key <= container.Nodes[i - 1].Key)
                {
                    problem = problem ?? $"key {node.Key} is not sorted";
                }

                if ((node.Key < minKey || node.Key >= maxKey) && (container.IsLeaf || i > 0))
                {
                    problem = problem ?? $"key {node.Key} is outside range {minKey} - {maxKey}";
                }

                if (container.IsLeaf)
                {
                    if (oldExtents.ContainsKey(node.Key))
                    {
                        problem = problem ?? $"extent {node.Key} exists more than once";
                        continue;
                    }

                    oldExtents[node.Key] = (ExtentBNode)node;
                    continue;
                }

                if (!InVolume(node.Data) || !ClaimAdmin(node.Data))
                {
                    problem = problem ?? $"child block {node.Data} is outside partition or used by another structure";
                    continue;
                }

                extentTreeBlocks.Add(node.Data);
                var childMin = i == 0 ? minKey : node.Key;
                var childMax = i < container.Nodes.Count - 1 ? container.Nodes[i + 1].Key : maxKey;
                if (!await ReadBNodeContainer(node.Data, childMin, childMax, false, depth + 1) &&
                    !IsInAdminSpace(node.Data))
                {
                    // invalid B-tree node container outside admin spaces, e.g. a data block, is not reused
                    adminUsed.Remove(node.Data);
                    extentTreeBlocks.Remove(node.Data);
                }
            }

            if (problem != null)
            {
                extentTreeValid = false;
                AddIssue(IssueType.BNodeContainerInvalid, block, null,
                    $"B-tree node container is invalid: {problem}, extent B-tree is rebuilt");
            }

            return true;
        }

        private ExtentBNode FindExtent(uint key, uint expectedPrev)
        {
            if (oldExtents.TryGetValue(key, out var extent))
            {
                return extent;
            }

            if (scannedExtents != null && scannedExtents.TryGetValue(key, out var list))
            {
                return list.FirstOrDefault(x => x.Prev == expectedPrev);
            }

            return null;
        }

        /// <summary>
        /// Check files: extent chains, file sizes and cross linked extents.
        /// </summary>
        private void CheckFiles()
        {
            ShowMsg("Checking files");

            var blockSize = (uint)volume.BlockSize;

            foreach (var file in files.ToList())
            {
                var sfsObject = file.Object;
                var chain = new List<ExtentBNode>();
                var key = sfsObject.Data;
                var expectedPrev = file.DiskNode | Constants.ExtentPrevObjectNodeFlag;
                var visited = new HashSet<uint>();
                string problem = null;
                var type = IssueType.ExtentInvalid;
                while (key != 0)
                {
                    if (!visited.Add(key))
                    {
                        problem = $"extent chain loops at extent {key}";
                        break;
                    }

                    var extent = FindExtent(key, expectedPrev);
                    if (extent == null)
                    {
                        problem = $"extent {key} doesn't exist";
                        break;
                    }

                    if (extent.Blocks == 0)
                    {
                        problem = $"extent {key} has zero blocks";
                        break;
                    }

                    if ((ulong)key + extent.Blocks > volume.Blocks)
                    {
                        problem = $"extent {key} with {extent.Blocks} blocks is outside partition";
                        break;
                    }

                    if (AnyInRange(metadataMap, key, extent.Blocks))
                    {
                        type = IssueType.ExtentOverlapsMetadata;
                        problem = $"extent {key} with {extent.Blocks} blocks uses reserved blocks, root blocks, bitmap or admin spaces";
                        break;
                    }

                    chain.Add(new ExtentBNode
                    {
                        Key = extent.Key, Next = extent.Next, Prev = extent.Prev, Blocks = extent.Blocks
                    });
                    expectedPrev = key;
                    key = extent.Next;
                }

                file.Extents = chain;

                if (problem != null)
                {
                    DropExtentChain(key);
                    var blocks = chain.Sum(x => (long)x.Blocks);
                    var newSize = (uint)Math.Min(sfsObject.Size, blocks * blockSize);
                    AddIssue(type, file.Container.Block, file.Path,
                        $"File has invalid extent: {problem}, file is truncated to {newSize} bytes");
                    sfsObject.Size = newSize;
                    file.Container.Dirty = true;
                    if (chain.Count > 0)
                    {
                        changedExtents.Add(chain[chain.Count - 1].Key);
                    }

                    continue;
                }

                var blocksNeeded = (sfsObject.Size + (long)blockSize - 1) / blockSize;
                var blocksUsed = chain.Sum(x => (long)x.Blocks);
                if (blocksUsed < blocksNeeded)
                {
                    var newSize = (uint)(blocksUsed * blockSize);
                    AddIssue(IssueType.FileSizeMismatch, file.Container.Block, file.Path,
                        $"File size {sfsObject.Size} needs {blocksNeeded} blocks, but extents have {blocksUsed} blocks, file size is set to {newSize}");
                    sfsObject.Size = newSize;
                    file.Container.Dirty = true;
                }
                else if (blocksUsed > blocksNeeded)
                {
                    AddIssue(IssueType.FileSizeMismatch, file.Container.Block, file.Path,
                        $"File size {sfsObject.Size} needs {blocksNeeded} blocks, but extents have {blocksUsed} blocks, extents are truncated");
                    var keep = blocksNeeded;
                    var newChain = new List<ExtentBNode>();
                    foreach (var extent in chain)
                    {
                        if (keep == 0)
                        {
                            droppedExtents.Add(extent.Key);
                            continue;
                        }

                        if (extent.Blocks > keep)
                        {
                            extent.Blocks = (ushort)keep;
                        }

                        changedExtents.Add(extent.Key);
                        keep -= extent.Blocks;
                        newChain.Add(extent);
                    }

                    file.Extents = newChain;
                }
            }

            // cross linked extents. Files with extent chains matching previous of extents are kept first, as previous
            // tells which file owns an extent, then newest files are kept. Other files using same blocks are removed.
            var claimed = CreateBlockMap();
            var owners = new List<(uint Start, uint Count, ObjModel File)>();
            foreach (var file in files.OrderByDescending(IsExtentChainConsistent)
                         .ThenByDescending(x => x.Object.DateModified).ThenBy(x => x.DiskNode).ToList())
            {
                var conflict = file.Extents.FirstOrDefault(x => AnyInRange(claimed, x.Key, x.Blocks));
                if (conflict != null)
                {
                    var other = owners.First(x =>
                        conflict.Key < (ulong)x.Start + x.Count && x.Start < (ulong)conflict.Key + conflict.Blocks);
                    var reason = IsExtentChainConsistent(other.File) && !IsExtentChainConsistent(file)
                        ? "file owning the extents"
                        : "newer file";
                    AddIssue(IssueType.CrossLinkedExtent, file.Container.Block, file.Path,
                        $"File uses blocks {conflict.Key} to {conflict.Key + conflict.Blocks - 1}, which are also used by {reason} '{other.File.Path}', file is removed");
                    RemoveObject(file);
                    continue;
                }

                foreach (var extent in file.Extents)
                {
                    MarkRange(claimed, extent.Key, extent.Blocks);
                    owners.Add((extent.Key, extent.Blocks, file));
                }
            }

            dataMap = claimed;

            // update extent chains and first data block
            foreach (var file in files)
            {
                var chain = file.Extents;
                for (var i = 0; i < chain.Count; i++)
                {
                    var prev = i == 0 ? file.Object.ObjectNode | Constants.ExtentPrevObjectNodeFlag : chain[i - 1].Key;
                    var next = i < chain.Count - 1 ? chain[i + 1].Key : 0;
                    if (i == 0 && file.NeedsNewNode)
                    {
                        changedExtents.Add(chain[i].Key);
                    }

                    chain[i].Prev = prev;
                    chain[i].Next = next;
                }

                var data = chain.Count > 0 ? chain[0].Key : 0;
                if (file.Object.Data != data)
                {
                    file.Object.Data = data;
                    file.Container.Dirty = true;
                }
            }
        }

        /// <summary>
        /// Previous of all extents of file matches extent chain read from disk, first extent has object node of file.
        /// </summary>
        private static bool IsExtentChainConsistent(ObjModel file)
        {
            var expectedPrev = file.DiskNode | Constants.ExtentPrevObjectNodeFlag;
            foreach (var extent in file.Extents)
            {
                if (extent.Prev != expectedPrev)
                {
                    return false;
                }

                expectedPrev = extent.Key;
            }

            return true;
        }

        /// <summary>
        /// Mark extents in chain from key as dropped, so they are not reported as not used.
        /// </summary>
        private void DropExtentChain(uint key)
        {
            var visited = new HashSet<uint>();
            while (key != 0 && visited.Add(key) && oldExtents.TryGetValue(key, out var extent))
            {
                droppedExtents.Add(key);
                key = extent.Next;
            }
        }

        /// <summary>
        /// Update extent B-tree to match extents of files. Extent B-tree is patched, if its structure is valid and it
        /// contains the same extents, otherwise it's rebuilt.
        /// </summary>
        private async Task FinalizeExtents()
        {
            var desired = new SortedDictionary<uint, ExtentBNode>();
            var fileByExtent = new Dictionary<uint, ObjModel>();
            foreach (var file in files)
            {
                foreach (var extent in file.Extents)
                {
                    desired[extent.Key] = extent;
                    fileByExtent[extent.Key] = file;
                }
            }

            var notUsed = oldExtents.Keys.Where(x => !desired.ContainsKey(x) && !droppedExtents.Contains(x))
                .OrderBy(x => x).ToList();
            if (notUsed.Count > 0)
            {
                AddIssue(IssueType.ExtentNotUsed, rootBlock.ExtentBNodeRoot, null,
                    $"{notUsed.Count} extents are not used by any file: {ListBlocks(notUsed)}", true);
            }

            foreach (var entry in desired)
            {
                if (!oldExtents.TryGetValue(entry.Key, out var oldExtent) || changedExtents.Contains(entry.Key))
                {
                    continue;
                }

                var extent = entry.Value;
                if (oldExtent.Next != extent.Next || oldExtent.Prev != extent.Prev || oldExtent.Blocks != extent.Blocks)
                {
                    var file = fileByExtent[entry.Key];
                    AddIssue(IssueType.ExtentChain, file.Container.Block, file.Path,
                        $"Extent {entry.Key} has next {oldExtent.Next}, prev 0x{oldExtent.Prev:x8}, expected next {extent.Next}, prev 0x{extent.Prev:x8}");
                }
            }

            var sameKeys = extentTreeValid && desired.Count == oldExtents.Count &&
                           desired.Keys.All(x => oldExtents.ContainsKey(x));
            if (sameKeys)
            {
                PatchExtentTree(desired);
            }
            else
            {
                RebuildExtentTree(desired);
            }

            await Task.CompletedTask;
        }

        private void PatchExtentTree(IDictionary<uint, ExtentBNode> desired)
        {
            foreach (var container in bNodeContainers.Where(x => x.IsLeaf))
            {
                var dirty = false;
                foreach (var node in container.Nodes.Cast<ExtentBNode>())
                {
                    var want = desired[node.Key];
                    if (node.Next != want.Next || node.Prev != want.Prev || node.Blocks != want.Blocks)
                    {
                        node.Next = want.Next;
                        node.Prev = want.Prev;
                        node.Blocks = want.Blocks;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    ChangeBlock(container.OwnBlock, BNodeContainerBuilder.Build(container, volume.BlockSize));
                }
            }
        }

        /// <summary>
        /// Rebuild extent B-tree. Root B-tree node container stays at its block, blocks of old B-tree node
        /// containers are reused and new B-tree node containers are allocated from admin spaces. Nodes are
        /// distributed evenly, so each container is at least half full.
        /// </summary>
        private void RebuildExtentTree(SortedDictionary<uint, ExtentBNode> desired)
        {
            ShowMsg("Rebuilding extent B-tree");

            var root = rootBlock.ExtentBNodeRoot;
            var pool = new Queue<uint>();
            foreach (var block in extentTreeBlocks)
            {
                adminUsed.Remove(block);
                pool.Enqueue(block);
            }

            ClaimAdmin(root);

            var nodes = desired.Values.Cast<BNode>().ToList();
            var isLeaf = true;
            while (nodes.Count > (isLeaf ? LeafBNodeCapacity : InternalBNodeCapacity))
            {
                var level = new List<BNode>();
                foreach (var group in SplitEvenly(nodes, isLeaf ? LeafBNodeCapacity : InternalBNodeCapacity))
                {
                    var block = AllocateAdminBlock(pool);
                    if (block == 0)
                    {
                        return;
                    }

                    WriteBNodeContainer(block, isLeaf, group);
                    level.Add(new BNode { Key = group[0].Key, Data = block });
                }

                nodes = level;
                isLeaf = false;
            }

            WriteBNodeContainer(root, isLeaf, nodes);
        }

        private void WriteBNodeContainer(uint block, bool isLeaf, IList<BNode> nodes)
        {
            var container = isLeaf ? BNodeContainer.CreateLeaf() : BNodeContainer.CreateInternal();
            container.OwnBlock = block;
            container.Nodes.AddRange(nodes);
            ChangeBlock(block, BNodeContainerBuilder.Build(container, volume.BlockSize));
        }

        private static IEnumerable<IList<T>> SplitEvenly<T>(IList<T> items, int capacity)
        {
            var groups = (items.Count + capacity - 1) / capacity;
            var size = items.Count / groups;
            var remainder = items.Count % groups;
            var offset = 0;
            for (var i = 0; i < groups; i++)
            {
                var count = size + (i < remainder ? 1 : 0);
                yield return items.Skip(offset).Take(count).ToList();
                offset += count;
            }
        }
    }
}
