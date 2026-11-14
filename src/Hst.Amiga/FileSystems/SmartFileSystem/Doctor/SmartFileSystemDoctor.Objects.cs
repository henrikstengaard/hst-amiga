namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Directory tree: object containers, objects, hash tables and soft links.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Object containers in directory tree including root object container.
        /// </summary>
        private readonly List<ContainerModel> objectContainers;

        private ContainerModel rootContainer;
        private ObjModel rootObj;

        /// <summary>
        /// Object node numbers used by objects found.
        /// </summary>
        private readonly HashSet<uint> objectNodesSeen = new HashSet<uint>();

        /// <summary>
        /// Object node numbers of objects removed by repair.
        /// </summary>
        private readonly HashSet<uint> removedNodes = new HashSet<uint>();

        /// <summary>
        /// All objects in directory tree, root directory first.
        /// </summary>
        private IEnumerable<ObjModel> AllObjects()
        {
            if (rootObj == null)
            {
                yield break;
            }

            var stack = new Stack<ObjModel>();
            stack.Push(rootObj);
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                yield return obj;
                for (var i = obj.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push(obj.Children[i]);
                }
            }
        }

        private static string CombinePath(string parent, string name) =>
            string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";

        /// <summary>
        /// Check directory tree starting with root object container.
        /// </summary>
        /// <returns>True, if directory tree could be checked.</returns>
        private async Task<bool> CheckDirectoryTree()
        {
            ShowMsg("Checking directory tree");

            var block = rootBlock.RootObjectContainer;
            ClaimAdmin(block);
            var data = await ReadBlock(block);
            var problem = GetBlockProblem(data, block, Constants.ObjectContainerId);
            ObjectContainer container = null;

            // a root object container with only a checksum error is used, if it contains the root directory
            if (problem == null || (GetUInt32(data, 0) == Constants.ObjectContainerId && GetUInt32(data, 8) == block))
            {
                container = ParseObjectContainer(data, block, true, out _);
                var root = container.Objects.FirstOrDefault();
                if (root == null || root.ObjectNode != Constants.RootNode || !root.IsDir)
                {
                    problem = "root object container doesn't contain root directory";
                    container = null;
                }
            }

            ContainerModel model;
            if (container == null)
            {
                AddIssue(IssueType.RootObjectContainerInvalid, block, null,
                    $"Root object container is invalid: {problem}, root object container is rebuilt");
                model = await RecoverRootContainer(block);
            }
            else
            {
                model = new ContainerModel { Block = block, Container = container, IsRoot = true };
                if (problem != null)
                {
                    AddIssue(IssueType.RootObjectContainerInvalid, block, null,
                        $"Root object container is invalid: {problem}, root object container is rewritten");
                    model.Dirty = true;
                }

                if (container.Objects.Count > 1)
                {
                    AddIssue(IssueType.ObjectCorrupt, block, null,
                        $"Root object container contains {container.Objects.Count - 1} objects after root directory, objects are removed");
                    while (container.Objects.Count > 1)
                    {
                        container.Objects.RemoveAt(container.Objects.Count - 1);
                    }

                    model.Dirty = true;
                }
            }

            rootContainer = model;
            objectContainers.Add(model);
            rootObj = new ObjModel
            {
                Object = model.Container.Objects[0],
                Container = model,
                Path = string.Empty,
                DiskNode = Constants.RootNode,
                HashTableRebuild = model.Container.Objects[0].HashTable == 0
            };
            objectNodesSeen.Add(Constants.RootNode);

            CheckName(rootObj, Constants.MaxVolumeNameLength);
            await CheckDirectory(rootObj, 0);
            return true;
        }

        /// <summary>
        /// Rebuild root object container. First object container of root directory is found by scanning admin
        /// spaces for object containers with root directory as parent, preferring the object container with most
        /// objects confirmed by object node tree. Volume name can't be recovered.
        /// </summary>
        private async Task<ContainerModel> RecoverRootContainer(uint block)
        {
            uint best = 0;
            var bestScore = -1;
            foreach (var adminSpace in AdminSpaces.ToList())
            {
                for (uint i = 0; i < Constants.BlocksAdmin; i++)
                {
                    var candidate = adminSpace.Space + i;
                    if (candidate == block || adminUsed.Contains(candidate))
                    {
                        continue;
                    }

                    var data = await ReadBlock(candidate);
                    if (!IsValidBlock(data, candidate, Constants.ObjectContainerId) ||
                        GetUInt32(data, 0xc) != Constants.RootNode || GetUInt32(data, 0x14) != 0)
                    {
                        continue;
                    }

                    var container = ParseObjectContainer(data, candidate, false, out _);
                    var score = container.Objects.Count(x =>
                        oldNodes.TryGetValue(x.ObjectNode, out var node) && node.Data == candidate);
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }

            var date = rootBlock.DateCreated;
            var rootContainerBlock = new ObjectContainer
            {
                OwnBlock = block,
                Parent = 0,
                RootInfo = new RootInfo { DateCreated = date }
            };
            rootContainerBlock.Objects.Add(new SfsObject
            {
                ObjectNode = Constants.RootNode,
                Protection = ProtectionHelper.DefaultProtection,
                DateModified = date,
                Bits = Constants.OTypeDir,
                HashTable = 0,
                FirstDirBlock = best,
                Name = "Recovered"
            });

            ShowMsg(best == 0
                ? "No object containers found for root directory"
                : $"Root directory uses object container at block {best}");

            return new ContainerModel { Block = block, Container = rootContainerBlock, IsRoot = true, Dirty = true };
        }

        /// <summary>
        /// Parse object container. Objects are read until an object is corrupt, e.g. name or comment isn't
        /// terminated before end of block.
        /// </summary>
        private ObjectContainer ParseObjectContainer(byte[] data, uint block, bool hasRootInfo, out string problem)
        {
            problem = null;
            var container = new ObjectContainer
            {
                OwnBlock = block,
                Parent = GetUInt32(data, 0xc),
                Next = GetUInt32(data, 0x10),
                Previous = GetUInt32(data, 0x14)
            };

            var endOffset = data.Length - (hasRootInfo ? Constants.RootInfoSize : 0);

            // objects exist while offset is less than block size - sizeof(struct fsObject) - 2 and first character of
            // name is not zero, 68k sizeof(struct fsObject) = 26 is used as in object container parser
            var objectsEndOffset = data.Length - 26 - 2;
            var offset = Constants.ObjectContainerHeaderSize;
            while (offset < objectsEndOffset && offset + Constants.ObjectHeaderSize < endOffset &&
                   data[offset + Constants.ObjectHeaderSize] != 0)
            {
                var nameEnd = Array.IndexOf(data, (byte)0, offset + Constants.ObjectHeaderSize,
                    endOffset - offset - Constants.ObjectHeaderSize);
                var commentEnd = nameEnd < 0 ? -1 : Array.IndexOf(data, (byte)0, nameEnd + 1, endOffset - nameEnd - 1);
                if (commentEnd < 0)
                {
                    problem = $"Object at offset {offset} is corrupt, name or comment is not terminated";
                    break;
                }

                var size = commentEnd + 1 - offset;
                if ((size & 1) == 1)
                {
                    size++;
                }

                container.Objects.Add(ObjectContainerParser.ParseObject(data, offset, out _));
                offset += size;
            }

            if (hasRootInfo)
            {
                container.RootInfo = ObjectContainerParser.ParseRootInfo(data, data.Length - Constants.RootInfoSize);
            }

            return container;
        }

        /// <summary>
        /// Check directory: hash table and object containers with objects.
        /// </summary>
        private async Task CheckDirectory(ObjModel dir, int depth)
        {
            var dirObject = dir.Object;
            if (dirObject.HashTable != 0)
            {
                var data = await ReadBlock(dirObject.HashTable);
                var problem = GetBlockProblem(data, dirObject.HashTable, Constants.HashTableId);
                if (problem == null && !ClaimAdmin(dirObject.HashTable))
                {
                    problem = $"block {dirObject.HashTable} is used by another structure";
                }

                if (problem == null)
                {
                    dir.HashTable = HashTableBlockParser.Parse(data);
                }
                else
                {
                    AddIssue(IssueType.HashTableInvalid, dirObject.HashTable, dir.Path,
                        $"Hash table is invalid: {problem}, hash table is rebuilt");
                    dir.HashTableRebuild = true;
                }
            }

            if (depth >= MaxDepth)
            {
                if (dirObject.FirstDirBlock != 0)
                {
                    AddIssue(IssueType.ObjectContainerInvalid, dirObject.FirstDirBlock, dir.Path,
                        $"Directory exceeds maximum depth {MaxDepth}, objects in directory are removed");
                }

                return;
            }

            var names = new Dictionary<string, ObjModel>();
            var block = dirObject.FirstDirBlock;
            var previous = 0U;
            while (block != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] data = null;
                var problem = InVolume(block)
                    ? GetBlockProblem(data = await ReadBlock(block), block, Constants.ObjectContainerId)
                    : $"block {block} is outside partition";

                // an object container with only a checksum error is used, if it's in the right directory and chain
                var checksumError = problem != null && IsChecksumOnlyProblem(data, block, Constants.ObjectContainerId) &&
                                    GetUInt32(data, 0xc) == dir.DiskNode && GetUInt32(data, 0x14) == previous;
                if (checksumError)
                {
                    problem = null;
                }

                if (problem == null && !ClaimAdmin(block))
                {
                    problem = $"block {block} is used by another directory or structure";
                }

                if (problem != null)
                {
                    AddIssue(IssueType.ObjectContainerInvalid, previous == 0 ? dir.Container.Block : previous,
                        dir.Path,
                        $"Object container is invalid: {problem}, objects in object container and following object containers are lost");
                    break;
                }

                var container = ParseObjectContainer(data, block, false, out var corrupt);
                var model = new ContainerModel { Block = block, Container = container, Dirty = checksumError };
                dir.DirContainers.Add(model);
                objectContainers.Add(model);

                if (checksumError)
                {
                    AddIssue(IssueType.ObjectContainerInvalid, block, dir.Path,
                        "Object container has invalid checksum, object container is rewritten");
                }

                if (container.Parent != dir.DiskNode)
                {
                    AddIssue(IssueType.ObjectContainerChain, block, dir.Path,
                        $"Object container has parent {container.Parent}, expected {dir.DiskNode}");
                }

                if (container.Previous != previous)
                {
                    AddIssue(IssueType.ObjectContainerChain, block, dir.Path,
                        $"Object container has previous {container.Previous}, expected {previous}");
                }

                if (corrupt != null)
                {
                    AddIssue(IssueType.ObjectCorrupt, block, dir.Path, $"{corrupt}, object and following objects are removed");
                    model.Dirty = true;
                }

                if (container.Objects.Count == 0)
                {
                    model.WasEmpty = true;
                    AddIssue(IssueType.ObjectContainerChain, block, dir.Path,
                        "Object container is empty, object container is removed from directory");
                }

                foreach (var sfsObject in container.Objects.ToList())
                {
                    await CheckObject(dir, model, sfsObject, names, depth);
                }

                previous = block;
                block = container.Next;
            }
        }

        /// <summary>
        /// Check object: name, comment, object node number and type specific structures.
        /// </summary>
        private async Task CheckObject(ObjModel dir, ContainerModel model, SfsObject sfsObject,
            IDictionary<string, ObjModel> names, int depth)
        {
            var obj = new ObjModel
            {
                Object = sfsObject,
                Container = model,
                Parent = dir,
                DiskNode = sfsObject.ObjectNode,
                Path = CombinePath(dir.Path, sfsObject.Name)
            };

            CheckName(obj, Constants.MaxNameLength);

            if (sfsObject.Comment.Length > Constants.MaxCommentLength)
            {
                AddIssue(IssueType.InvalidComment, model.Block, obj.Path,
                    $"Comment is {sfsObject.Comment.Length} characters, comment is truncated to {Constants.MaxCommentLength} characters");
                sfsObject.Comment = sfsObject.Comment.Substring(0, Constants.MaxCommentLength);
                model.Dirty = true;
            }

            var node = sfsObject.ObjectNode;
            var isRecycled = node == Constants.RecycledNode && dir.IsRootObject && sfsObject.IsDir;
            if (node == 0 || node == Constants.RootNode || (node <= 6 && !isRecycled) ||
                (node & Constants.ExtentPrevObjectNodeFlag) != 0)
            {
                AddIssue(IssueType.ObjectNodeNumberInvalid, model.Block, obj.Path,
                    $"Object node {node} is invalid, new object node is allocated");
                obj.NeedsNewNode = true;
            }
            else if (!objectNodesSeen.Add(node))
            {
                AddIssue(IssueType.ObjectNodeNumberInvalid, model.Block, obj.Path,
                    $"Object node {node} is used by more than one object, new object node is allocated");
                obj.NeedsNewNode = true;
            }

            var key = UpperName(sfsObject.Name);
            if (names.TryGetValue(key, out var existing))
            {
                var oldName = sfsObject.Name;
                var index = 1;
                string newName;
                do
                {
                    newName = CreateName(oldName, $"_{index++}", Math.Max(oldName.Length, Constants.MaxNameLength));
                } while (names.ContainsKey(UpperName(newName)));

                var fits = TryRename(obj, newName);
                AddIssue(IssueType.DuplicateName, model.Block, obj.Path,
                    fits
                        ? $"Name is also used by '{existing.Path}', object is renamed to '{newName}'"
                        : $"Name is also used by '{existing.Path}', object can't be renamed as object container is full",
                    fixable: fits);
            }

            names[UpperName(sfsObject.Name)] = obj;
            dir.Children.Add(obj);

            if (sfsObject.IsDir)
            {
                await CheckDirectory(obj, depth + 1);
            }
            else if (sfsObject.IsSoftLink)
            {
                var data = await ReadBlock(sfsObject.Data);
                var problem = GetBlockProblem(data, sfsObject.Data, Constants.SoftLinkId);

                // a soft link block with only a checksum error is used, if it belongs to the soft link
                var checksumError = problem != null &&
                                    IsChecksumOnlyProblem(data, sfsObject.Data, Constants.SoftLinkId) &&
                                    GetUInt32(data, 0xc) == obj.DiskNode;
                if (checksumError)
                {
                    problem = null;
                }

                if (problem == null && !ClaimAdmin(sfsObject.Data))
                {
                    problem = $"block {sfsObject.Data} is used by another structure";
                }

                if (problem != null)
                {
                    AddIssue(IssueType.SoftLinkInvalid, model.Block, obj.Path,
                        $"Soft link block is invalid: {problem}, soft link is removed");
                    RemoveObject(obj);
                    return;
                }

                obj.SoftLink = SoftLinkBlockParser.Parse(data);
                if (checksumError)
                {
                    AddIssue(IssueType.SoftLinkInvalid, sfsObject.Data, obj.Path,
                        "Soft link block has invalid checksum, soft link block is rewritten");
                    obj.SoftLinkDirty = true;
                }
            }
            else if (sfsObject.IsHardLink)
            {
                AddIssue(IssueType.UnsupportedObject, model.Block, obj.Path,
                    $"Object type with bits 0x{sfsObject.Bits:x2} is not supported", true, false);
            }
            else
            {
                files.Add(obj);
            }
        }

        /// <summary>
        /// Check name of object: max length and no ':' or '/'.
        /// </summary>
        private void CheckName(ObjModel obj, int maxLength)
        {
            var name = obj.Object.Name;
            var newName = name.Replace(':', '_').Replace('/', '_');
            if (newName.Length > maxLength)
            {
                newName = newName.Substring(0, maxLength);
            }

            if (newName == name)
            {
                return;
            }

            obj.Object.Name = newName;
            obj.Container.Dirty = true;
            AddIssue(IssueType.InvalidName, obj.Container.Block, obj.Path,
                $"Name '{name}' is invalid, it's too long or contains ':' or '/', object is renamed to '{newName}'");
        }

        private static string CreateName(string name, string suffix, int maxLength)
        {
            if (name.Length + suffix.Length > maxLength)
            {
                name = name.Substring(0, Math.Max(0, maxLength - suffix.Length));
            }

            return name + suffix;
        }

        /// <summary>
        /// Rename object, if new name fits in object container.
        /// </summary>
        private bool TryRename(ObjModel obj, string newName)
        {
            var oldName = obj.Object.Name;
            obj.Object.Name = newName;
            var endOffset = volume.BlockSize - (obj.Container.IsRoot ? Constants.RootInfoSize : 0);
            if (obj.Container.Container.GetEmptySpaceOffset() > endOffset)
            {
                obj.Object.Name = oldName;
                return false;
            }

            obj.Container.Dirty = true;
            return true;
        }

        /// <summary>
        /// Remove object from directory tree. Administration blocks and extents used by object are freed.
        /// </summary>
        private void RemoveObject(ObjModel obj)
        {
            obj.Removed = true;
            obj.Container.Container.Objects.Remove(obj.Object);
            obj.Container.Dirty = true;
            if (obj.Parent != null)
            {
                obj.Parent.Children.Remove(obj);
                obj.Parent.ChildrenRemoved = true;
            }

            files.Remove(obj);
            if (!obj.NeedsNewNode)
            {
                removedNodes.Add(obj.DiskNode);
            }

            if (obj.SoftLink != null)
            {
                adminUsed.Remove(obj.Object.Data);
            }

            foreach (var extent in obj.Extents)
            {
                droppedExtents.Add(extent.Key);
            }

            foreach (var child in obj.Children.ToList())
            {
                RemoveObject(child);
            }

            foreach (var container in obj.DirContainers)
            {
                adminUsed.Remove(container.Block);
                objectContainers.Remove(container);
            }

            if (obj.HashTable != null)
            {
                adminUsed.Remove(obj.Object.HashTable);
            }
        }

        /// <summary>
        /// Update object container chains of directories: parent, previous and next. Empty object containers are
        /// removed from directories.
        /// </summary>
        private void FinalizeDirectories()
        {
            foreach (var dir in AllObjects().Where(x => x.Object.IsDir).ToList())
            {
                var kept = dir.DirContainers.Where(x => x.Container.Objects.Count > 0).ToList();
                foreach (var container in dir.DirContainers.Where(x => x.Container.Objects.Count == 0))
                {
                    adminUsed.Remove(container.Block);
                    objectContainers.Remove(container);
                }

                for (var i = 0; i < kept.Count; i++)
                {
                    var container = kept[i].Container;
                    var previous = i > 0 ? kept[i - 1].Block : 0;
                    var next = i < kept.Count - 1 ? kept[i + 1].Block : 0;
                    if (container.Parent != dir.Object.ObjectNode || container.Previous != previous ||
                        container.Next != next)
                    {
                        container.Parent = dir.Object.ObjectNode;
                        container.Previous = previous;
                        container.Next = next;
                        kept[i].Dirty = true;
                    }
                }

                var first = kept.Count > 0 ? kept[0].Block : 0;
                if (dir.Object.FirstDirBlock != first)
                {
                    dir.Object.FirstDirBlock = first;
                    dir.Container.Dirty = true;
                }
            }
        }

        /// <summary>
        /// Check and rebuild hash tables of directories. Hash chains must contain all objects in directory in the
        /// hash chain of their name hash.
        /// </summary>
        private async Task FinalizeHashTables()
        {
            var entries = HashHelper.HashEntries(volume.BlockSize);
            foreach (var dir in AllObjects().Where(x => x.Object.IsDir).ToList())
            {
                var children = dir.Children;
                if (dir.Object.HashTable == 0 && !dir.HashTableRebuild)
                {
                    // directory without hash table, e.g. recycled directory
                    foreach (var child in children)
                    {
                        ObjectNode oldNode = null;
                        if (!child.NeedsNewNode)
                        {
                            oldNodes.TryGetValue(child.DiskNode, out oldNode);
                        }

                        child.HashNext = oldNode?.Next ?? 0;
                        child.Hash16 = oldNode?.Hash16 ?? HashHelper.Hash(child.Object.Name, caseSensitive);
                    }

                    continue;
                }

                // hash chains from existing hash table, where objects are in right hash chain
                var chains = new List<ObjModel>[entries];
                for (var i = 0; i < entries; i++)
                {
                    chains[i] = new List<ObjModel>();
                }

                var placed = new HashSet<ObjModel>();
                var byDiskNode = new Dictionary<uint, ObjModel>();
                foreach (var child in children.Where(x => !x.NeedsNewNode))
                {
                    byDiskNode[child.DiskNode] = child;
                }

                if (dir.HashTable != null)
                {
                    for (var chain = 0; chain < entries; chain++)
                    {
                        var node = dir.HashTable.HashEntries[chain];
                        var visited = new HashSet<uint>();
                        while (node != 0 && visited.Add(node) && byDiskNode.TryGetValue(node, out var child) &&
                               !placed.Contains(child) &&
                               HashHelper.HashChain(HashHelper.Hash(child.Object.Name, caseSensitive),
                                   volume.BlockSize) == chain &&
                               oldNodes.TryGetValue(node, out var objectNode))
                        {
                            chains[chain].Add(child);
                            placed.Add(child);
                            node = objectNode.Next;
                        }
                    }
                }

                // objects not in hash chains are added first in their hash chain
                foreach (var child in children.Where(x => !placed.Contains(x)))
                {
                    var hash = HashHelper.Hash(child.Object.Name, caseSensitive);
                    chains[HashHelper.HashChain(hash, volume.BlockSize)].Insert(0, child);
                }

                var hashEntries = new uint[entries];
                for (var chain = 0; chain < entries; chain++)
                {
                    var list = chains[chain];
                    hashEntries[chain] = list.Count > 0 ? list[0].Object.ObjectNode : 0;
                    for (var i = 0; i < list.Count; i++)
                    {
                        list[i].HashNext = i < list.Count - 1 ? list[i + 1].Object.ObjectNode : 0;
                        list[i].Hash16 = HashHelper.Hash(list[i].Object.Name, caseSensitive);
                    }
                }

                var rebuild = dir.HashTableRebuild;
                if (!rebuild)
                {
                    // mismatches caused by new object nodes or object nodes lost with an invalid object node tree
                    // are fixed without reporting them
                    var mismatch = dir.HashTable.Parent != dir.Object.ObjectNode ||
                                   !dir.HashTable.HashEntries.SequenceEqual(hashEntries);
                    var reportable = dir.HashTable.Parent != dir.DiskNode;
                    var nodesLost = false;
                    foreach (var child in children)
                    {
                        if (child.NeedsNewNode || !oldNodes.TryGetValue(child.DiskNode, out var oldNode))
                        {
                            mismatch = true;
                            nodesLost |= !child.NeedsNewNode && !nodeTreeValid;
                            continue;
                        }

                        if (oldNode.Next != child.HashNext || oldNode.Hash16 != child.Hash16)
                        {
                            mismatch = true;
                        }
                    }

                    if (mismatch)
                    {
                        rebuild = true;
                        if (reportable ||
                            (!nodesLost && !dir.ChildrenRemoved && children.All(x => !x.NeedsNewNode)))
                        {
                            AddIssue(IssueType.HashChainInvalid, dir.Object.HashTable, dir.Path,
                                "Hash chains don't match objects in directory, hash table is rebuilt");
                        }
                    }
                }

                if (!rebuild)
                {
                    continue;
                }

                var block = dir.HashTable != null ? dir.Object.HashTable : 0;
                if (block == 0)
                {
                    var oldBlock = dir.Object.HashTable;
                    block = oldBlock != 0 && InVolume(oldBlock) && IsInAdminSpace(oldBlock) && ClaimAdmin(oldBlock)
                        ? oldBlock
                        : AllocateAdminBlock();
                }

                if (block == 0)
                {
                    continue;
                }

                var hashTable = new HashTableBlock(volume.BlockSize)
                {
                    OwnBlock = block,
                    Parent = dir.Object.ObjectNode,
                    HashEntries = hashEntries
                };
                ChangeBlock(block, HashTableBlockBuilder.Build(hashTable, volume.BlockSize));

                if (dir.Object.HashTable != block)
                {
                    dir.Object.HashTable = block;
                    dir.Container.Dirty = true;
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Update parent of soft link blocks.
        /// </summary>
        private Task FinalizeSoftLinks()
        {
            foreach (var obj in AllObjects().Where(x => x.SoftLink != null))
            {
                if (obj.SoftLink.Parent == obj.Object.ObjectNode && !obj.SoftLinkDirty)
                {
                    continue;
                }

                if (obj.SoftLink.Parent != obj.DiskNode)
                {
                    AddIssue(IssueType.SoftLinkInvalid, obj.Object.Data, obj.Path,
                        $"Soft link block has parent {obj.SoftLink.Parent}, expected {obj.DiskNode}");
                }

                obj.SoftLink.Parent = obj.Object.ObjectNode;
                try
                {
                    ChangeBlock(obj.Object.Data, SoftLinkBlockBuilder.Build(obj.SoftLink, volume.BlockSize));
                }
                catch (IOException e)
                {
                    AddIssue(IssueType.SoftLinkInvalid, obj.Object.Data, obj.Path, e.Message, fixable: false);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Write object containers changed.
        /// </summary>
        private void WriteObjectContainers()
        {
            foreach (var model in objectContainers.Where(x => x.Dirty))
            {
                model.Container.OwnBlock = model.Block;
                try
                {
                    ChangeBlock(model.Block, ObjectContainerBuilder.Build(model.Container, volume.BlockSize));
                }
                catch (IOException e)
                {
                    AddIssue(IssueType.ObjectContainerInvalid, model.Block, null,
                        $"Object container can't be written: {e.Message}", fixable: false);
                }
            }
        }
    }
}
