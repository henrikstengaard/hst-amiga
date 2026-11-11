namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Extent B-tree, see fs/btreenodes.c in smart file system source.
    /// </summary>
    public static class ExtentBTree
    {
        public class BNodeResult
        {
            public BNodeContainer Container { get; set; }

            /// <summary>
            /// Index of node in container, -1 if container is empty.
            /// </summary>
            public int Index { get; set; }

            public BNode Node => Index >= 0 ? Container.Nodes[Index] : null;
        }

        /// <summary>
        /// Search for node equal to key or slightly lower, searchforbnode() in smart file system source.
        /// Returns first node, if no node is lower than key.
        /// </summary>
        public static int SearchForBNode(BNodeContainer container, uint key)
        {
            var n = container.Nodes.Count - 1;
            while (n > 0 && key < container.Nodes[n].Key)
            {
                n--;
            }

            return n;
        }

        /// <summary>
        /// Find node with key or closest match, findbnode() in smart file system source.
        /// </summary>
        public static async Task<BNodeResult> FindBNode(Volume volume, uint rootBlock, uint key)
        {
            var block = rootBlock;
            for (;;)
            {
                var container = await Disk.ReadBNodeContainer(volume, block);
                if (container.Nodes.Count == 0)
                {
                    return new BNodeResult { Container = container, Index = -1 };
                }

                var index = SearchForBNode(container, key);
                if (container.IsLeaf)
                {
                    return new BNodeResult { Container = container, Index = index };
                }

                block = container.Nodes[index].Data;
            }
        }

        /// <summary>
        /// Find extent node with exact key.
        /// </summary>
        public static async Task<BNodeResult> FindExtent(Volume volume, uint key)
        {
            var result = await FindBNode(volume, volume.RootBlock.ExtentBNodeRoot, key);
            if (result.Node == null || result.Node.Key != key || !(result.Node is ExtentBNode))
            {
                throw new IOException($"Extent with key {key} not found");
            }

            return result;
        }

        private static void InsertBNode(BNodeContainer container, BNode node)
        {
            var index = 0;
            while (index < container.Nodes.Count && container.Nodes[index].Key < node.Key)
            {
                index++;
            }

            container.Nodes.Insert(index, node);
        }

        /// <summary>
        /// Insert node in B-tree, createbnode() in smart file system source.
        /// </summary>
        public static async Task CreateBNode(Volume volume, uint rootBlock, BNode node)
        {
            for (;;)
            {
                var result = await FindBNode(volume, rootBlock, node.Key);
                var container = result.Container;

                if (result.Node != null && result.Node.Key == node.Key)
                {
                    throw new IOException($"B-tree node with key {node.Key} already exists");
                }

                if (container.Nodes.Count < container.Capacity(volume.BlockSize))
                {
                    InsertBNode(container, node);
                    await Disk.WriteBNodeContainer(volume, container);
                    return;
                }

                await SplitBTreeContainer(volume, rootBlock, container);
            }
        }

        /// <summary>
        /// Get parent of container, getparentbtreecontainer() in smart file system source.
        /// </summary>
        /// <returns>Parent container or null, if container is root.</returns>
        private static async Task<BNodeContainer> GetParent(Volume volume, uint rootBlock, BNodeContainer child,
            uint? searchKey = null)
        {
            if (rootBlock == child.OwnBlock)
            {
                return null;
            }

            var childKey = searchKey ?? (child.Nodes.Count > 0 ? child.Nodes[0].Key : 0);
            var block = rootBlock;
            for (;;)
            {
                var container = await Disk.ReadBNodeContainer(volume, block);
                if (container.IsLeaf)
                {
                    throw new IOException($"Couldn't find parent for B-tree node container at block {child.OwnBlock}");
                }

                if (container.Nodes.Any(x => x.Data == child.OwnBlock))
                {
                    return container;
                }

                block = container.Nodes[SearchForBNode(container, childKey)].Data;
            }
        }

        /// <summary>
        /// Split full container, splitbtreecontainer() in smart file system source.
        /// </summary>
        private static async Task SplitBTreeContainer(Volume volume, uint rootBlock, BNodeContainer container)
        {
            var parent = await GetParent(volume, rootBlock, container);
            if (parent == null)
            {
                // container is root, move root content to a new block and make root an internal container
                // with one node pointing to new block. this way root block stays the same.
                var copyBlock = await AdminSpaces.Allocate(volume);
                var copy = new BNodeContainer
                {
                    OwnBlock = copyBlock,
                    IsLeaf = container.IsLeaf,
                    NodeSize = container.NodeSize,
                    Nodes = container.Nodes.ToList()
                };
                await Disk.WriteBNodeContainer(volume, copy);

                parent = BNodeContainer.CreateInternal();
                parent.OwnBlock = rootBlock;
                parent.Nodes.Add(new BNode { Key = 0, Data = copyBlock });
                await Disk.WriteBNodeContainer(volume, parent);

                container = copy;
            }

            if (parent.Nodes.Count == parent.Capacity(volume.BlockSize))
            {
                // parent must be split first
                await SplitBTreeContainer(volume, rootBlock, parent);
                parent = await GetParent(volume, rootBlock, container);
            }

            var branches = container.Capacity(volume.BlockSize);
            var half = branches / 2;
            var newBlock = await AdminSpaces.Allocate(volume);
            var newContainer = new BNodeContainer
            {
                OwnBlock = newBlock,
                IsLeaf = container.IsLeaf,
                NodeSize = container.NodeSize,
                Nodes = container.Nodes.Skip(half).ToList()
            };
            await Disk.WriteBNodeContainer(volume, newContainer);

            container.Nodes = container.Nodes.Take(half).ToList();
            await Disk.WriteBNodeContainer(volume, container);

            InsertBNode(parent, new BNode { Key = newContainer.Nodes[0].Key, Data = newBlock });
            await Disk.WriteBNodeContainer(volume, parent);
        }

        /// <summary>
        /// Delete node with key, deletebnode() in smart file system source.
        /// </summary>
        public static async Task DeleteBNode(Volume volume, uint rootBlock, uint key)
        {
            var result = await FindBNode(volume, rootBlock, key);
            if (result.Node == null || result.Node.Key != key)
            {
                throw new IOException($"B-tree node with key {key} doesn't exist");
            }

            await DeleteInternalNode(volume, rootBlock, result.Container, key);
        }

        /// <summary>
        /// Delete node from container and merge or steal nodes from neighbour containers, if container doesn't
        /// contain enough nodes, deleteinternalnode() in smart file system source.
        /// </summary>
        private static async Task DeleteInternalNode(Volume volume, uint rootBlock, BNodeContainer container, uint key)
        {
            var branches = container.Capacity(volume.BlockSize);

            // key used to find parent container, first key before node is removed
            var searchKey = container.Nodes.Count > 0 ? container.Nodes[0].Key : key;

            var index = container.Nodes.FindIndex(x => x.Key == key);
            if (index >= 0)
            {
                container.Nodes.RemoveAt(index);
            }

            await Disk.WriteBNodeContainer(volume, container);

            if (container.Nodes.Count >= (branches + 1) / 2)
            {
                return;
            }

            var parent = await GetParent(volume, rootBlock, container, searchKey);
            if (parent == null)
            {
                // container is root
                if (container.Nodes.Count == 1 && !container.IsLeaf)
                {
                    // root has only 1 node, copy child into root and free child. root block stays the same.
                    var childBlock = container.Nodes[0].Data;
                    var child = await Disk.ReadBNodeContainer(volume, childBlock);
                    child.OwnBlock = rootBlock;
                    await Disk.WriteBNodeContainer(volume, child);
                    await AdminSpaces.Free(volume, childBlock);
                }

                return;
            }

            var n = parent.Nodes.FindIndex(x => x.Data == container.OwnBlock);

            if (n < parent.Nodes.Count - 1)
            {
                // next neighbour
                var next = await Disk.ReadBNodeContainer(volume, parent.Nodes[n + 1].Data);
                if (next.Nodes.Count + container.Nodes.Count > branches)
                {
                    // merging is not possible, steal nodes from next
                    var nodesToSteal = (next.Nodes.Count + container.Nodes.Count) / 2 - container.Nodes.Count;
                    container.Nodes.AddRange(next.Nodes.Take(nodesToSteal));
                    await Disk.WriteBNodeContainer(volume, container);

                    next.Nodes = next.Nodes.Skip(nodesToSteal).ToList();
                    await Disk.WriteBNodeContainer(volume, next);

                    parent.Nodes[n + 1].Key = next.Nodes[0].Key;
                    await Disk.WriteBNodeContainer(volume, parent);
                }
                else
                {
                    // merge next into container
                    container.Nodes.AddRange(next.Nodes);
                    await Disk.WriteBNodeContainer(volume, container);
                    await AdminSpaces.Free(volume, next.OwnBlock);
                    await DeleteInternalNode(volume, rootBlock, parent, parent.Nodes[n + 1].Key);
                }
            }
            else if (n > 0)
            {
                // previous neighbour
                var previous = await Disk.ReadBNodeContainer(volume, parent.Nodes[n - 1].Data);
                if (previous.Nodes.Count + container.Nodes.Count > branches)
                {
                    // merging is not possible, steal nodes from previous
                    var nodesToSteal = (previous.Nodes.Count + container.Nodes.Count) / 2 - container.Nodes.Count;
                    var stolenNodes = previous.Nodes.Skip(previous.Nodes.Count - nodesToSteal).ToList();
                    container.Nodes.InsertRange(0, stolenNodes);
                    await Disk.WriteBNodeContainer(volume, container);

                    previous.Nodes = previous.Nodes.Take(previous.Nodes.Count - nodesToSteal).ToList();
                    await Disk.WriteBNodeContainer(volume, previous);

                    parent.Nodes[n].Key = container.Nodes[0].Key;
                    await Disk.WriteBNodeContainer(volume, parent);
                }
                else
                {
                    // merge container into previous
                    previous.Nodes.AddRange(container.Nodes);
                    await Disk.WriteBNodeContainer(volume, previous);
                    await AdminSpaces.Free(volume, container.OwnBlock);
                    await DeleteInternalNode(volume, rootBlock, parent, parent.Nodes[n].Key);
                }
            }
        }
    }
}
