namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;
    using Exceptions;

    /// <summary>
    /// Object info with object and object container containing object.
    /// </summary>
    public class ObjectInfo
    {
        public ObjectContainer Container { get; set; }
        public SfsObject Object { get; set; }

        /// <summary>
        /// Object node of parent directory, 0 for root.
        /// </summary>
        public uint ParentNode => Container.Parent;
    }

    /// <summary>
    /// Objects (files, directories and soft links), see fs/objects.c in smart file system source.
    /// </summary>
    public static class Objects
    {
        /// <summary>
        /// Read object by object node, readobject() in smart file system source.
        /// </summary>
        public static async Task<ObjectInfo> ReadObject(Volume volume, uint nodeNumber)
        {
            var nodeResult = await NodeTree.FindNode(volume, nodeNumber);
            var data = nodeResult.Node.Data;
            if (data == 0 || data == Constants.ReservedNode)
            {
                throw new IOException($"Object node {nodeNumber} is not in use");
            }

            var container = await Disk.ReadObjectContainer(volume, data);
            var sfsObject = container.Objects.FirstOrDefault(x => x.ObjectNode == nodeNumber);
            if (sfsObject == null)
            {
                throw new IOException($"Object with object node {nodeNumber} not found in object container block {data}");
            }

            return new ObjectInfo { Container = container, Object = sfsObject };
        }

        /// <summary>
        /// List objects in directory.
        /// </summary>
        public static async Task<IList<ObjectInfo>> ListObjects(Volume volume, SfsObject dir)
        {
            var objects = new List<ObjectInfo>();
            var visited = new HashSet<uint>();
            var block = dir.FirstDirBlock;
            while (block != 0 && visited.Add(block))
            {
                var container = await Disk.ReadObjectContainer(volume, block);
                objects.AddRange(container.Objects.Select(x => new ObjectInfo { Container = container, Object = x }));
                block = container.Next;
            }

            return objects;
        }

        /// <summary>
        /// Find object by name in directory, scandir()/locateobject() in smart file system source.
        /// </summary>
        /// <returns>Object info, null if not found.</returns>
        public static async Task<ObjectInfo> FindObject(Volume volume, SfsObject dir, string name)
        {
            if (!dir.IsDir)
            {
                throw new IOException($"Object '{dir.Name}' is not a directory");
            }

            if (dir.HashTable == 0)
            {
                return (await ListObjects(volume, dir)).FirstOrDefault(x =>
                    HashHelper.NamesEqual(x.Object.Name, name, volume.CaseSensitive));
            }

            var hashTable = await Disk.ReadHashTable(volume, dir.HashTable);
            var hash = HashHelper.Hash(name, volume.CaseSensitive);
            var nodeNumber = hashTable.HashEntries[HashHelper.HashChain(hash, volume.BlockSize)];
            var visited = new HashSet<uint>();
            while (nodeNumber != 0 && visited.Add(nodeNumber))
            {
                var nodeResult = await NodeTree.FindNode(volume, nodeNumber);
                if (nodeResult.Node.Hash16 == hash)
                {
                    var container = await Disk.ReadObjectContainer(volume, nodeResult.Node.Data);
                    var sfsObject = container.Objects.FirstOrDefault(x => x.ObjectNode == nodeNumber);
                    if (sfsObject != null && HashHelper.NamesEqual(sfsObject.Name, name, volume.CaseSensitive))
                    {
                        return new ObjectInfo { Container = container, Object = sfsObject };
                    }
                }

                nodeNumber = nodeResult.Node.Next;
            }

            return null;
        }

        /// <summary>
        /// Update object with fixed size changes, e.g. protection, date or size.
        /// </summary>
        public static async Task UpdateObject(Volume volume, uint nodeNumber, Action<SfsObject> update)
        {
            var objectInfo = await ReadObject(volume, nodeNumber);
            update(objectInfo.Object);
            if (!objectInfo.Container.Fits(volume.BlockSize))
            {
                throw new IOException($"Object '{objectInfo.Object.Name}' doesn't fit in object container");
            }

            await Disk.WriteObjectContainer(volume, objectInfo.Container);
        }

        /// <summary>
        /// Update date and clear archive bit of directory, bumpobject() in smart file system source.
        /// </summary>
        public static async Task BumpObject(Volume volume, uint dirNode)
        {
            if (dirNode == 0)
            {
                return;
            }

            await UpdateObject(volume, dirNode, o =>
            {
                o.DateModified = DateTime.Now;
                o.Protection &= ~(uint)ProtectionBits.Archive;
            });
        }

        /// <summary>
        /// Create object in directory, createobjecttags() in smart file system source.
        /// Creates hash table for directories and soft link block for soft links.
        /// </summary>
        public static async Task<uint> CreateObject(Volume volume, uint dirNode, SfsObject newObject,
            string softLinkPath = null)
        {
            ValidateName(newObject.Name);
            ValidateComment(newObject.Comment);

            var dirInfo = await ReadObject(volume, dirNode);
            if (!dirInfo.Object.IsDir)
            {
                throw new PathNotFoundException($"Object '{dirInfo.Object.Name}' is not a directory");
            }

            if (await FindObject(volume, dirInfo.Object, newObject.Name) != null)
            {
                throw new PathAlreadyExistsException($"Path '{newObject.Name}' already exists");
            }

            var nodeResult = await NodeTree.CreateNode(volume);
            var nodeNumber = nodeResult.NodeNumber;
            newObject.ObjectNode = nodeNumber;

            if (newObject.IsDir)
            {
                var hashTableBlock = await AdminSpaces.Allocate(volume);
                await Disk.WriteHashTable(volume, new HashTableBlock(volume.BlockSize)
                {
                    OwnBlock = hashTableBlock,
                    Parent = nodeNumber
                });
                newObject.HashTable = hashTableBlock;
                newObject.FirstDirBlock = 0;
            }
            else if (newObject.IsSoftLink)
            {
                var softLinkBlock = await AdminSpaces.Allocate(volume);
                await Disk.WriteSoftLink(volume, new SoftLinkBlock
                {
                    OwnBlock = softLinkBlock,
                    Parent = nodeNumber,
                    Path = softLinkPath ?? string.Empty
                });
                newObject.Data = softLinkBlock;
                newObject.Size = 0;
            }

            await AttachObject(volume, dirNode, newObject);
            await BumpObject(volume, dirNode);

            return nodeNumber;
        }

        /// <summary>
        /// Attach existing object with object node to directory by inserting it in an object container and
        /// directory hash table.
        /// </summary>
        public static async Task AttachObject(Volume volume, uint dirNode, SfsObject sfsObject)
        {
            var containerBlock = await InsertIntoDirectory(volume, dirNode, sfsObject);

            var dir = (await ReadObject(volume, dirNode)).Object;
            if (dir.HashTable == 0)
            {
                await NodeTree.UpdateNode(volume, sfsObject.ObjectNode, node =>
                {
                    node.Data = containerBlock;
                    node.Next = 0;
                });
                return;
            }

            var hash = HashHelper.Hash(sfsObject.Name, volume.CaseSensitive);
            var hashChain = HashHelper.HashChain(hash, volume.BlockSize);
            var hashTable = await Disk.ReadHashTable(volume, dir.HashTable);

            await NodeTree.UpdateNode(volume, sfsObject.ObjectNode, node =>
            {
                node.Data = containerBlock;
                node.Hash16 = hash;
                node.Next = hashTable.HashEntries[hashChain];
            });

            hashTable.HashEntries[hashChain] = sfsObject.ObjectNode;
            await Disk.WriteHashTable(volume, hashTable);
        }

        /// <summary>
        /// Insert object in first object container of directory with enough free space or in a new object container,
        /// findobjectspace() in smart file system source.
        /// </summary>
        /// <returns>Object container block object was inserted in.</returns>
        private static async Task<uint> InsertIntoDirectory(Volume volume, uint dirNode, SfsObject sfsObject)
        {
            var dir = (await ReadObject(volume, dirNode)).Object;
            var bytesNeeded = sfsObject.GetBytesNeeded();

            var visited = new HashSet<uint>();
            var block = dir.FirstDirBlock;
            while (block != 0 && visited.Add(block))
            {
                var container = await Disk.ReadObjectContainer(volume, block);
                if (volume.BlockSize - container.GetEmptySpaceOffset() >= bytesNeeded)
                {
                    container.Objects.Add(sfsObject);
                    await Disk.WriteObjectContainer(volume, container);
                    return block;
                }

                if (dir.IsQuickDir)
                {
                    break;
                }

                block = container.Next;
            }

            // no object container with enough space, create new object container first in directory
            var newBlock = await AdminSpaces.Allocate(volume);
            var firstBlock = dir.FirstDirBlock;
            var newContainer = new ObjectContainer
            {
                OwnBlock = newBlock,
                Parent = dirNode,
                Next = firstBlock,
                Previous = 0
            };
            newContainer.Objects.Add(sfsObject);
            await Disk.WriteObjectContainer(volume, newContainer);

            if (firstBlock != 0)
            {
                var firstContainer = await Disk.ReadObjectContainer(volume, firstBlock);
                firstContainer.Previous = newBlock;
                await Disk.WriteObjectContainer(volume, firstContainer);
            }

            await UpdateObject(volume, dirNode, o => o.FirstDirBlock = newBlock);

            return newBlock;
        }

        /// <summary>
        /// Detach object from directory by removing it from directory hash table and object container.
        /// Object node and data of object are kept.
        /// </summary>
        public static async Task DetachObject(Volume volume, ObjectInfo objectInfo)
        {
            var dirNode = objectInfo.ParentNode;
            var dir = (await ReadObject(volume, dirNode)).Object;

            await RemoveFromHashChain(volume, dir, objectInfo.Object);

            // re-read object info as directory and object could share object container
            objectInfo = await ReadObject(volume, objectInfo.Object.ObjectNode);
            await RemoveFromContainer(volume, objectInfo);
        }

        private static async Task RemoveFromHashChain(Volume volume, SfsObject dir, SfsObject sfsObject)
        {
            if (dir.HashTable == 0)
            {
                return;
            }

            var hashTable = await Disk.ReadHashTable(volume, dir.HashTable);
            var hashChain = HashHelper.HashChain(HashHelper.Hash(sfsObject.Name, volume.CaseSensitive), volume.BlockSize);
            var nodeResult = await NodeTree.FindNode(volume, sfsObject.ObjectNode);
            var next = nodeResult.Node.Next;

            if (hashTable.HashEntries[hashChain] == sfsObject.ObjectNode)
            {
                hashTable.HashEntries[hashChain] = next;
                await Disk.WriteHashTable(volume, hashTable);
                return;
            }

            var visited = new HashSet<uint>();
            var nodeNumber = hashTable.HashEntries[hashChain];
            while (nodeNumber != 0 && visited.Add(nodeNumber))
            {
                var previousNode = await NodeTree.FindNode(volume, nodeNumber);
                if (previousNode.Node.Next == sfsObject.ObjectNode)
                {
                    previousNode.Node.Next = next;
                    await Disk.WriteNodeContainer(volume, previousNode.Container);
                    return;
                }

                nodeNumber = previousNode.Node.Next;
            }

            throw new IOException($"Object '{sfsObject.Name}' with object node {sfsObject.ObjectNode} not found in hash chain");
        }

        /// <summary>
        /// Remove object from object container, removeobject() in smart file system source.
        /// Object container is removed from directory and freed, if object is the only object in it.
        /// </summary>
        private static async Task RemoveFromContainer(Volume volume, ObjectInfo objectInfo)
        {
            var container = objectInfo.Container;
            if (container.Objects.Count > 1)
            {
                container.Objects.Remove(objectInfo.Object);
                await Disk.WriteObjectContainer(volume, container);
                return;
            }

            if (container.Previous != 0)
            {
                var previousContainer = await Disk.ReadObjectContainer(volume, container.Previous);
                previousContainer.Next = container.Next;
                await Disk.WriteObjectContainer(volume, previousContainer);
            }
            else
            {
                await UpdateObject(volume, container.Parent, o => o.FirstDirBlock = container.Next);
            }

            if (container.Next != 0)
            {
                var nextContainer = await Disk.ReadObjectContainer(volume, container.Next);
                nextContainer.Previous = container.Previous;
                await Disk.WriteObjectContainer(volume, nextContainer);
            }

            await AdminSpaces.Free(volume, container.OwnBlock);
        }

        /// <summary>
        /// Delete object and free blocks used by it, deleteobject() in smart file system source.
        /// </summary>
        public static async Task DeleteObject(Volume volume, ObjectInfo objectInfo)
        {
            var sfsObject = objectInfo.Object;
            var dirNode = objectInfo.ParentNode;

            if (sfsObject.ObjectNode == Constants.RootNode)
            {
                throw new FileSystemException("Root directory can't be deleted");
            }

            if (sfsObject.IsDir && sfsObject.FirstDirBlock != 0)
            {
                throw new DirectoryNotEmptyException($"Directory '{sfsObject.Name}' is not empty");
            }

            await DetachObject(volume, objectInfo);
            await NodeTree.DeleteNode(volume, sfsObject.ObjectNode);

            if (sfsObject.IsDir)
            {
                if (sfsObject.HashTable != 0)
                {
                    await AdminSpaces.Free(volume, sfsObject.HashTable);
                }
            }
            else if (sfsObject.IsSoftLink)
            {
                if (sfsObject.Data != 0)
                {
                    await AdminSpaces.Free(volume, sfsObject.Data);
                }
            }
            else
            {
                await FreeExtents(volume, sfsObject.Data);

                if (dirNode == Constants.RecycledNode)
                {
                    var blocks = (uint)((sfsObject.Size + (ulong)volume.BlockSize - 1) / (ulong)volume.BlockSize);
                    await Disk.UpdateRootInfo(volume, rootInfo =>
                    {
                        rootInfo.DeletedFiles = rootInfo.DeletedFiles > 0 ? rootInfo.DeletedFiles - 1 : 0;
                        rootInfo.DeletedBlocks = rootInfo.DeletedBlocks > blocks ? rootInfo.DeletedBlocks - blocks : 0;
                    });
                }
            }

            await BumpObject(volume, dirNode);
        }

        /// <summary>
        /// Free all extents starting with extent key, deleteextents() in smart file system source.
        /// </summary>
        public static async Task FreeExtents(Volume volume, uint key)
        {
            var visited = new HashSet<uint>();
            while (key != 0 && visited.Add(key))
            {
                var result = await ExtentBTree.FindExtent(volume, key);
                var extent = (ExtentBNode)result.Node;
                var next = extent.Next;
                await Bitmap.MarkFree(volume, extent.Key, extent.Blocks);
                await ExtentBTree.DeleteBNode(volume, volume.RootBlock.ExtentBNodeRoot, extent.Key);
                key = next;
            }
        }

        /// <summary>
        /// Read extents of file starting with extent key.
        /// </summary>
        public static async Task<IList<ExtentBNode>> ReadExtents(Volume volume, uint key)
        {
            var extents = new List<ExtentBNode>();
            var visited = new HashSet<uint>();
            while (key != 0 && visited.Add(key))
            {
                var result = await ExtentBTree.FindExtent(volume, key);
                var extent = (ExtentBNode)result.Node;
                extents.Add(extent);
                key = extent.Next;
            }

            return extents;
        }

        /// <summary>
        /// Get parent directory object node of object.
        /// </summary>
        public static async Task<uint> GetParentNode(Volume volume, uint nodeNumber)
        {
            if (nodeNumber == Constants.RootNode)
            {
                return 0;
            }

            return (await ReadObject(volume, nodeNumber)).ParentNode;
        }

        public static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name is empty", nameof(name));
            }

            if (name.IndexOf('/') >= 0 || name.IndexOf(':') >= 0)
            {
                throw new ArgumentException($"Name '{name}' contains invalid characters", nameof(name));
            }

            if (AmigaTextHelper.GetBytes(name).Length > Constants.MaxNameLength)
            {
                throw new ArgumentException($"Name '{name}' is longer than {Constants.MaxNameLength} characters",
                    nameof(name));
            }
        }

        public static void ValidateComment(string comment)
        {
            if (comment != null && AmigaTextHelper.GetBytes(comment).Length > Constants.MaxCommentLength)
            {
                throw new ArgumentException($"Comment is longer than {Constants.MaxCommentLength} characters",
                    nameof(comment));
            }
        }
    }
}
