namespace Hst.Amiga.FileSystems.SmartFileSystem
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Object node tree, see fs/nodes.c in smart file system source.
    /// </summary>
    public static class NodeTree
    {
        public class NodeResult
        {
            public NodeContainer Container { get; set; }
            public int Index { get; set; }
            public uint NodeNumber { get; set; }
            public ObjectNode Node => Container.ObjectNodes[Index];
        }

        private static uint ToBlock(Volume volume, uint pointer) => pointer >> volume.ShiftsBlock32;
        private static uint ToPointer(Volume volume, uint block) => block << volume.ShiftsBlock32;

        /// <summary>
        /// Find object node by node number, findnode() in smart file system source.
        /// </summary>
        public static async Task<NodeResult> FindNode(Volume volume, uint nodeNumber)
        {
            var block = volume.RootBlock.ObjectNodeRoot;
            for (;;)
            {
                var nodeContainer = await Disk.ReadNodeContainer(volume, block);
                if (nodeNumber < nodeContainer.NodeNumber)
                {
                    throw new IOException($"Object node {nodeNumber} not found");
                }

                if (nodeContainer.IsLeaf)
                {
                    var index = (int)(nodeNumber - nodeContainer.NodeNumber);
                    if (index >= nodeContainer.ObjectNodes.Length)
                    {
                        throw new IOException($"Object node {nodeNumber} not found");
                    }

                    return new NodeResult { Container = nodeContainer, Index = index, NodeNumber = nodeNumber };
                }

                var pointerIndex = (nodeNumber - nodeContainer.NodeNumber) / nodeContainer.Nodes;
                if (pointerIndex >= nodeContainer.Pointers.Length || nodeContainer.Pointers[pointerIndex] == 0)
                {
                    throw new IOException($"Object node {nodeNumber} not found");
                }

                block = ToBlock(volume, nodeContainer.Pointers[pointerIndex]);
            }
        }

        /// <summary>
        /// Create new object node, createnode() in smart file system source.
        /// Node data is set to reserved value, caller is responsible for updating node and writing node container.
        /// </summary>
        public static async Task<NodeResult> CreateNode(Volume volume)
        {
            var root = volume.RootBlock.ObjectNodeRoot;
            var leafCapacity = NodeContainer.LeafCapacity(volume.BlockSize);
            var block = root;

            for (;;)
            {
                var nodeContainer = await Disk.ReadNodeContainer(volume, block);
                if (nodeContainer.IsLeaf)
                {
                    var index = Array.FindIndex(nodeContainer.ObjectNodes, x => x.Data == 0);
                    if (index >= 0)
                    {
                        // mark node as reserved until caller updates it
                        nodeContainer.ObjectNodes[index].Data = Constants.ReservedNode;
                        nodeContainer.ObjectNodes[index].Next = 0;
                        nodeContainer.ObjectNodes[index].Hash16 = 0;
                        await Disk.WriteNodeContainer(volume, nodeContainer);

                        if (nodeContainer.ObjectNodes.All(x => x.Data != 0))
                        {
                            await MarkParentFull(volume, nodeContainer);
                        }

                        return new NodeResult
                        {
                            Container = await Disk.ReadNodeContainer(volume, block),
                            Index = index,
                            NodeNumber = nodeContainer.NodeNumber + (uint)index
                        };
                    }

                    if (block != root)
                    {
                        throw new IOException(
                            $"Couldn't find empty object node in node container at block {block} while node index container indicated there should be one");
                    }

                    await AddNewNodeLevel(volume);
                    block = root;
                    continue;
                }

                var notFullIndex = Array.FindIndex(nodeContainer.Pointers, x => x != 0 && (x & 1) == 0);
                if (notFullIndex >= 0)
                {
                    block = ToBlock(volume, nodeContainer.Pointers[notFullIndex]);
                    continue;
                }

                var unusedIndex = Array.FindIndex(nodeContainer.Pointers, x => x == 0);
                if (unusedIndex >= 0)
                {
                    var nodes = nodeContainer.Nodes == leafCapacity
                        ? 1U
                        : nodeContainer.Nodes / (uint)NodeContainer.IndexCapacity(volume.BlockSize);
                    var nodeNumber = nodeContainer.NodeNumber + (uint)unusedIndex * nodeContainer.Nodes;
                    var newBlock = await AdminSpaces.Allocate(volume);
                    var newNodeContainer = nodes == 1
                        ? NodeContainer.CreateLeaf(volume.BlockSize, nodeNumber)
                        : NodeContainer.CreateIndex(volume.BlockSize, nodeNumber, nodes);
                    newNodeContainer.OwnBlock = newBlock;
                    await Disk.WriteNodeContainer(volume, newNodeContainer);

                    nodeContainer = await Disk.ReadNodeContainer(volume, block);
                    nodeContainer.Pointers[unusedIndex] = ToPointer(volume, newBlock);
                    await Disk.WriteNodeContainer(volume, nodeContainer);
                    continue;
                }

                if (block != root)
                {
                    throw new IOException($"Node index container at block {block} is full, but parent indicated it's not");
                }

                await AddNewNodeLevel(volume);
                block = root;
            }
        }

        /// <summary>
        /// Update object node.
        /// </summary>
        public static async Task UpdateNode(Volume volume, uint nodeNumber, Action<ObjectNode> update)
        {
            var nodeResult = await FindNode(volume, nodeNumber);
            update(nodeResult.Node);
            await Disk.WriteNodeContainer(volume, nodeResult.Container);
        }

        /// <summary>
        /// Delete object node, deletenode() in smart file system source.
        /// </summary>
        public static async Task DeleteNode(Volume volume, uint nodeNumber)
        {
            var nodeResult = await FindNode(volume, nodeNumber);
            var nodeContainer = nodeResult.Container;
            nodeResult.Node.Data = 0;
            nodeResult.Node.Next = 0;
            nodeResult.Node.Hash16 = 0;
            await Disk.WriteNodeContainer(volume, nodeContainer);

            var empty = nodeContainer.ObjectNodes.Count(x => x.Data == 0);
            if (empty == 1)
            {
                // node container was completely full before, mark it not full
                await MarkParentEmpty(volume, nodeContainer);
            }
            else if (empty == nodeContainer.ObjectNodes.Length)
            {
                // node container is now completely empty, free it
                await FreeContainer(volume, nodeContainer);
            }
        }

        private static async Task<NodeContainer> GetParent(Volume volume, NodeContainer child)
        {
            var block = volume.RootBlock.ObjectNodeRoot;
            NodeContainer parent = null;
            while (block != child.OwnBlock)
            {
                parent = await Disk.ReadNodeContainer(volume, block);
                if (parent.IsLeaf)
                {
                    throw new IOException($"Failed to locate parent node container of block {child.OwnBlock}");
                }

                var index = (child.NodeNumber - parent.NodeNumber) / parent.Nodes;
                block = ToBlock(volume, parent.Pointers[index]);
            }

            return parent;
        }

        private static bool IsFull(NodeContainer nodeContainer) =>
            nodeContainer.Pointers.All(x => x != 0 && (x & 1) == 1);

        private static async Task MarkParentFull(Volume volume, NodeContainer nodeContainer)
        {
            var parent = await GetParent(volume, nodeContainer);
            if (parent == null)
            {
                return;
            }

            parent.Pointers[(nodeContainer.NodeNumber - parent.NodeNumber) / parent.Nodes] |= 1;
            await Disk.WriteNodeContainer(volume, parent);

            if (IsFull(parent))
            {
                await MarkParentFull(volume, parent);
            }
        }

        private static async Task MarkParentEmpty(Volume volume, NodeContainer nodeContainer)
        {
            var parent = await GetParent(volume, nodeContainer);
            if (parent == null)
            {
                return;
            }

            var wasFull = IsFull(parent);
            parent.Pointers[(nodeContainer.NodeNumber - parent.NodeNumber) / parent.Nodes] &= ~1U;
            await Disk.WriteNodeContainer(volume, parent);

            if (wasFull)
            {
                await MarkParentEmpty(volume, parent);
            }
        }

        private static async Task FreeContainer(Volume volume, NodeContainer nodeContainer)
        {
            // root node container is never freed as it doesn't have a parent
            var parent = await GetParent(volume, nodeContainer);
            if (parent == null)
            {
                return;
            }

            var index = (nodeContainer.NodeNumber - parent.NodeNumber) / parent.Nodes;
            await AdminSpaces.Free(volume, ToBlock(volume, parent.Pointers[index]));
            parent.Pointers[index] = 0;
            await Disk.WriteNodeContainer(volume, parent);

            if (parent.Pointers.All(x => x == 0))
            {
                await FreeContainer(volume, parent);
            }
        }

        /// <summary>
        /// Add new level to node tree, addnewnodelevel() in smart file system source.
        /// </summary>
        private static async Task AddNewNodeLevel(Volume volume)
        {
            var root = await Disk.ReadNodeContainer(volume, volume.RootBlock.ObjectNodeRoot);
            var newBlock = await AdminSpaces.Allocate(volume);

            // copy current root to new block
            root.OwnBlock = newBlock;
            await Disk.WriteNodeContainer(volume, root);

            var nodes = root.IsLeaf
                ? (uint)NodeContainer.LeafCapacity(volume.BlockSize)
                : root.Nodes * (uint)NodeContainer.IndexCapacity(volume.BlockSize);
            var newRoot = NodeContainer.CreateIndex(volume.BlockSize, root.NodeNumber, nodes);
            newRoot.OwnBlock = volume.RootBlock.ObjectNodeRoot;
            newRoot.Pointers[0] = ToPointer(volume, newBlock) | 1;
            await Disk.WriteNodeContainer(volume, newRoot);
        }
    }
}
