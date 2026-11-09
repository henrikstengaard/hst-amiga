namespace Hst.Amiga.Tests.SmartFileSystemTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileSystems.SmartFileSystem;
using FileSystems.SmartFileSystem.Blocks;

/// <summary>
/// Verifies consistency of a smart file system volume by walking all structures.
/// Checks object containers, hash chains, object nodes, extents, admin space bits and bitmap.
/// </summary>
public static class SmartFileSystemVerifier
{
    public static async Task<IList<string>> Verify(Volume volume, uint preAlloc)
    {
        var errors = new List<string>();
        var rootBlock = volume.RootBlock;
        var blockSize = volume.BlockSize;

        // metadata blocks referenced, which must be marked used in admin spaces
        var adminBlocks = new HashSet<uint> { Transactions.GetTransactionBlock(rootBlock) };

        void AddAdminBlock(uint block, string type)
        {
            if (!adminBlocks.Add(block))
            {
                errors.Add($"{type} block {block} is referenced more than once");
            }
        }

        // second root block
        try
        {
            Disk.CheckBlock(await Disk.ReadBlock(volume, volume.TotalBlocks - 1), volume.TotalBlocks - 1,
                Constants.RootBlockId);
        }
        catch (IOException e)
        {
            errors.Add(e.Message);
        }

        // admin space containers
        var adminSpaces = new List<AdminSpace>();
        var adminSpaceBlock = rootBlock.AdminSpaceContainer;
        var previousAdminSpaceBlock = 0U;
        while (adminSpaceBlock != 0)
        {
            var adminSpaceContainer = await Disk.ReadAdminSpaceContainer(volume, adminSpaceBlock);
            AddAdminBlock(adminSpaceBlock, "Admin space container");
            if (adminSpaceContainer.Previous != previousAdminSpaceBlock)
            {
                errors.Add($"Admin space container {adminSpaceBlock} has previous {adminSpaceContainer.Previous}, expected {previousAdminSpaceBlock}");
            }

            adminSpaces.AddRange(adminSpaceContainer.AdminSpaces.Where(x => x.Space != 0));
            previousAdminSpaceBlock = adminSpaceBlock;
            adminSpaceBlock = adminSpaceContainer.Next;
        }

        // node tree
        var objectNodes = new Dictionary<uint, ObjectNode>();
        await VerifyNodeContainer(volume, rootBlock.ObjectNodeRoot, null, objectNodes, errors, AddAdminBlock);

        // extent b-tree
        var extents = new Dictionary<uint, ExtentBNode>();
        await VerifyBNodeContainer(volume, rootBlock.ExtentBNodeRoot, 0, uint.MaxValue, extents, errors, AddAdminBlock, true);

        // objects
        var usedExtents = new HashSet<uint>();
        var reachableNodes = new HashSet<uint>();
        var rootContainer = await Disk.ReadObjectContainer(volume, rootBlock.RootObjectContainer);
        AddAdminBlock(rootBlock.RootObjectContainer, "Root object container");
        var rootObject = rootContainer.Objects.FirstOrDefault();
        if (rootObject == null || rootObject.ObjectNode != Constants.RootNode || !rootObject.IsDir)
        {
            errors.Add("Root object container doesn't contain root directory");
            return errors;
        }

        reachableNodes.Add(Constants.RootNode);
        if (!objectNodes.TryGetValue(Constants.RootNode, out var rootNode) || rootNode.Data != rootBlock.RootObjectContainer)
        {
            errors.Add("Root object node doesn't point to root object container");
        }

        await VerifyDirectory(volume, rootObject, objectNodes, extents, usedExtents, reachableNodes, errors, AddAdminBlock);

        foreach (var node in objectNodes.Where(x => x.Value.Data != 0 && x.Value.Data != Constants.ReservedNode))
        {
            if (!reachableNodes.Contains(node.Key))
            {
                errors.Add($"Object node {node.Key} is in use, but object is not reachable");
            }
        }

        foreach (var extent in extents.Values.Where(x => !usedExtents.Contains(x.Key)))
        {
            errors.Add($"Extent {extent.Key} is not used by any file");
        }

        // admin space bits must match referenced metadata blocks
        var adminUsed = new HashSet<uint>();
        foreach (var adminSpace in adminSpaces)
        {
            for (var bit = 0; bit < 32; bit++)
            {
                if ((adminSpace.Bits & (0x80000000U >> bit)) != 0)
                {
                    adminUsed.Add(adminSpace.Space + (uint)bit);
                }
            }
        }

        foreach (var block in adminBlocks.Where(x => !adminUsed.Contains(x)))
        {
            errors.Add($"Metadata block {block} is not marked used in admin space");
        }

        foreach (var block in adminUsed.Where(x => !adminBlocks.Contains(x)))
        {
            errors.Add($"Admin space block {block} is marked used, but not referenced");
        }

        // bitmap must match used blocks
        var startFree = rootBlock.BitmapBase + (uint)volume.BitmapBlocks.Length;
        var endFree = volume.TotalBlocks - Math.Max(preAlloc, 1);
        var expectedUsed = new bool[volume.TotalBlocks];
        for (uint block = 0; block < volume.TotalBlocks; block++)
        {
            expectedUsed[block] = block < startFree || block >= endFree;
        }

        foreach (var adminSpace in adminSpaces)
        {
            for (uint block = adminSpace.Space; block < adminSpace.Space + 32; block++)
            {
                if (block >= startFree && expectedUsed[block])
                {
                    errors.Add($"Admin space block {block} overlaps other used blocks");
                }

                expectedUsed[block] = true;
            }
        }

        foreach (var extent in extents.Values)
        {
            for (var block = extent.Key; block < extent.Key + extent.Blocks; block++)
            {
                if (expectedUsed[block])
                {
                    errors.Add($"Extent {extent.Key} block {block} overlaps other used blocks");
                }

                expectedUsed[block] = true;
            }
        }

        uint freeBlocks = 0;
        for (uint block = 0; block < volume.TotalBlocks; block++)
        {
            var free = Bitmap.IsFree(volume, block);
            if (free)
            {
                freeBlocks++;
            }

            if (free == expectedUsed[block])
            {
                errors.Add($"Block {block} is {(free ? "free" : "used")} in bitmap, but expected {(free ? "used" : "free")}");
                if (errors.Count > 100)
                {
                    return errors;
                }
            }
        }

        if (rootContainer.RootInfo.FreeBlocks != freeBlocks)
        {
            errors.Add($"Root info free blocks {rootContainer.RootInfo.FreeBlocks} doesn't match {freeBlocks} free blocks in bitmap");
        }

        return errors;
    }

    private static async Task VerifyDirectory(Volume volume, SfsObject dir, IDictionary<uint, ObjectNode> objectNodes,
        IDictionary<uint, ExtentBNode> extents, ISet<uint> usedExtents, ISet<uint> reachableNodes, IList<string> errors,
        Action<uint, string> addAdminBlock)
    {
        HashTableBlock hashTable = null;
        if (dir.HashTable != 0)
        {
            hashTable = await Disk.ReadHashTable(volume, dir.HashTable);
            addAdminBlock(dir.HashTable, "Hash table");
            if (hashTable.Parent != dir.ObjectNode)
            {
                errors.Add($"Hash table {dir.HashTable} has parent {hashTable.Parent}, expected {dir.ObjectNode}");
            }
        }

        var objectsInHashChains = new HashSet<uint>();
        if (hashTable != null)
        {
            for (var chain = 0; chain < hashTable.HashEntries.Length; chain++)
            {
                var node = hashTable.HashEntries[chain];
                var visited = new HashSet<uint>();
                while (node != 0)
                {
                    if (!visited.Add(node) || !objectNodes.TryGetValue(node, out var objectNode))
                    {
                        errors.Add($"Invalid hash chain {chain} in directory '{dir.Name}' at node {node}");
                        break;
                    }

                    if (HashHelper.HashChain(objectNode.Hash16, volume.BlockSize) != chain)
                    {
                        errors.Add($"Object node {node} with hash {objectNode.Hash16} is in wrong hash chain {chain}");
                    }

                    objectsInHashChains.Add(node);
                    node = objectNode.Next;
                }
            }
        }

        var block = dir.FirstDirBlock;
        var previous = 0U;
        var objectsInDir = new HashSet<uint>();
        while (block != 0)
        {
            var container = await Disk.ReadObjectContainer(volume, block);
            addAdminBlock(block, "Object container");

            if (container.Parent != dir.ObjectNode)
            {
                errors.Add($"Object container {block} has parent {container.Parent}, expected {dir.ObjectNode}");
            }

            if (container.Previous != previous)
            {
                errors.Add($"Object container {block} has previous {container.Previous}, expected {previous}");
            }

            if (container.Objects.Count == 0)
            {
                errors.Add($"Object container {block} is empty");
            }

            foreach (var sfsObject in container.Objects)
            {
                objectsInDir.Add(sfsObject.ObjectNode);
                if (!reachableNodes.Add(sfsObject.ObjectNode))
                {
                    errors.Add($"Object node {sfsObject.ObjectNode} is used by more than one object");
                }

                if (!objectNodes.TryGetValue(sfsObject.ObjectNode, out var objectNode) || objectNode.Data != block)
                {
                    errors.Add($"Object node {sfsObject.ObjectNode} for '{sfsObject.Name}' doesn't point to object container {block}");
                }
                else if (hashTable != null &&
                         objectNode.Hash16 != HashHelper.Hash(sfsObject.Name, volume.CaseSensitive))
                {
                    errors.Add($"Object node {sfsObject.ObjectNode} for '{sfsObject.Name}' has invalid hash");
                }

                if (sfsObject.IsDir)
                {
                    await VerifyDirectory(volume, sfsObject, objectNodes, extents, usedExtents, reachableNodes, errors,
                        addAdminBlock);
                }
                else if (sfsObject.IsSoftLink)
                {
                    var softLink = await Disk.ReadSoftLink(volume, sfsObject.Data);
                    addAdminBlock(sfsObject.Data, "Soft link");
                    if (softLink.Parent != sfsObject.ObjectNode)
                    {
                        errors.Add($"Soft link block {sfsObject.Data} has invalid parent");
                    }
                }
                else
                {
                    VerifyFileExtents(volume, sfsObject, extents, usedExtents, errors);
                }
            }

            previous = block;
            block = container.Next;
        }

        if (hashTable != null && !objectsInDir.SetEquals(objectsInHashChains))
        {
            errors.Add($"Objects in directory '{dir.Name}' doesn't match objects in hash chains");
        }
    }

    private static void VerifyFileExtents(Volume volume, SfsObject file, IDictionary<uint, ExtentBNode> extents,
        ISet<uint> usedExtents, IList<string> errors)
    {
        var key = file.Data;
        var expectedPrev = file.ObjectNode | Constants.ExtentPrevObjectNodeFlag;
        long blocks = 0;
        while (key != 0)
        {
            if (!extents.TryGetValue(key, out var extent))
            {
                errors.Add($"Extent {key} for file '{file.Name}' not found");
                return;
            }

            if (!usedExtents.Add(key))
            {
                errors.Add($"Extent {key} is used more than once");
                return;
            }

            if (extent.Prev != expectedPrev)
            {
                errors.Add($"Extent {key} for file '{file.Name}' has prev {extent.Prev:x8}, expected {expectedPrev:x8}");
            }

            blocks += extent.Blocks;
            expectedPrev = key;
            key = extent.Next;
        }

        var expectedBlocks = (file.Size + (long)volume.BlockSize - 1) / volume.BlockSize;
        if (blocks != expectedBlocks)
        {
            errors.Add($"File '{file.Name}' with size {file.Size} has {blocks} blocks in extents, expected {expectedBlocks}");
        }
    }

    private static async Task<bool> VerifyNodeContainer(Volume volume, uint block, NodeContainer parent,
        IDictionary<uint, ObjectNode> objectNodes, IList<string> errors, Action<uint, string> addAdminBlock)
    {
        var nodeContainer = await Disk.ReadNodeContainer(volume, block);
        addAdminBlock(block, "Node container");

        if (nodeContainer.IsLeaf)
        {
            for (var i = 0; i < nodeContainer.ObjectNodes.Length; i++)
            {
                objectNodes[nodeContainer.NodeNumber + (uint)i] = nodeContainer.ObjectNodes[i];
            }

            return nodeContainer.ObjectNodes.All(x => x.Data != 0);
        }

        var full = true;
        for (var i = 0; i < nodeContainer.Pointers.Length; i++)
        {
            var pointer = nodeContainer.Pointers[i];
            if (pointer == 0)
            {
                full = false;
                continue;
            }

            var childBlock = pointer >> volume.ShiftsBlock32;
            var child = await Disk.ReadNodeContainer(volume, childBlock);
            if (child.NodeNumber != nodeContainer.NodeNumber + (uint)i * nodeContainer.Nodes)
            {
                errors.Add($"Node container {childBlock} has node number {child.NodeNumber}, expected {nodeContainer.NodeNumber + (uint)i * nodeContainer.Nodes}");
            }

            var childFull = await VerifyNodeContainer(volume, childBlock, nodeContainer, objectNodes, errors, addAdminBlock);
            if (childFull != ((pointer & 1) == 1))
            {
                errors.Add($"Node container {block} pointer {i} has full bit {(pointer & 1)}, but child is {(childFull ? "full" : "not full")}");
            }

            full &= childFull;
        }

        return full;
    }

    private static async Task VerifyBNodeContainer(Volume volume, uint block, uint minKey, uint maxKey,
        IDictionary<uint, ExtentBNode> extents, IList<string> errors, Action<uint, string> addAdminBlock, bool isRoot)
    {
        var container = await Disk.ReadBNodeContainer(volume, block);
        addAdminBlock(block, "B-tree node container");

        if (!isRoot && container.Nodes.Count == 0)
        {
            errors.Add($"B-tree node container {block} is empty");
        }

        for (var i = 0; i < container.Nodes.Count; i++)
        {
            var node = container.Nodes[i];
            if (i > 0 && node.Key <= container.Nodes[i - 1].Key)
            {
                errors.Add($"B-tree node container {block} keys are not sorted");
            }

            if (node.Key < minKey || node.Key >= maxKey)
            {
                if (!(container.IsLeaf == false && i == 0))
                {
                    errors.Add($"B-tree node container {block} key {node.Key} is outside range {minKey} - {maxKey}");
                }
            }

            if (container.IsLeaf)
            {
                extents[node.Key] = (ExtentBNode)node;
            }
            else
            {
                var childMax = i < container.Nodes.Count - 1 ? container.Nodes[i + 1].Key : maxKey;
                var childMin = i == 0 ? minKey : node.Key;
                await VerifyBNodeContainer(volume, node.Data, childMin, childMax, extents, errors, addAdminBlock, false);
            }
        }
    }
}
