namespace Hst.Amiga.FileSystems.SmartFileSystem.Doctor
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Blocks;

    /// <summary>
    /// Object node tree, which maps object node numbers to object containers.
    /// </summary>
    public partial class SmartFileSystemDoctor
    {
        /// <summary>
        /// Object nodes read from valid leaf node containers.
        /// </summary>
        private readonly Dictionary<uint, ObjectNode> oldNodes;

        /// <summary>
        /// Valid node containers in object node tree.
        /// </summary>
        private readonly List<NodeContainer> nodeContainers;

        /// <summary>
        /// Blocks referenced by object node tree except root node container, valid or not. Blocks are reused when
        /// object node tree is rebuilt.
        /// </summary>
        private readonly List<uint> nodeTreeBlocks = new List<uint>();

        /// <summary>
        /// Object node tree structure is valid.
        /// </summary>
        private bool nodeTreeValid = true;

        /// <summary>
        /// Object nodes allocated by repair.
        /// </summary>
        private readonly HashSet<uint> newNodes;

        private int LeafCapacity => NodeContainer.LeafCapacity(volume.BlockSize);
        private int IndexCapacity => NodeContainer.IndexCapacity(volume.BlockSize);

        /// <summary>
        /// Read object node tree.
        /// </summary>
        private async Task ReadNodeTree()
        {
            ShowMsg("Checking object node tree");

            // root node container is claimed with other structures with fixed location
            await ReadNodeContainer(rootBlock.ObjectNodeRoot, 1, null, 0);
        }

        /// <summary>
        /// Read node container and its children.
        /// </summary>
        /// <returns>True, if node container is full. False, if it's not full. Null, if it's invalid.</returns>
        private async Task<bool?> ReadNodeContainer(uint block, ulong expectedNodeNumber, uint? expectedNodes,
            int depth)
        {
            var data = await ReadBlock(block);
            var problem = GetBlockProblem(data, block, Constants.NodeContainerId);
            NodeContainer container = null;
            if (problem == null)
            {
                container = NodeContainerParser.Parse(data);
                if (container.NodeNumber != expectedNodeNumber)
                {
                    problem = $"node number is {container.NodeNumber}, expected {expectedNodeNumber}";
                }
                else if (expectedNodes.HasValue && container.Nodes != expectedNodes.Value)
                {
                    problem = $"nodes is {container.Nodes}, expected {expectedNodes.Value}";
                }
                else if (!expectedNodes.HasValue && !IsValidRootNodes(container.Nodes))
                {
                    problem = $"nodes {container.Nodes} is invalid";
                }
                else if (depth > 16)
                {
                    problem = "object node tree is too deep";
                }
            }

            if (problem != null)
            {
                nodeTreeValid = false;
                AddIssue(IssueType.NodeContainerInvalid, block, null,
                    $"Node container is invalid: {problem}, object node tree is rebuilt");
                return null;
            }

            nodeContainers.Add(container);

            if (container.IsLeaf)
            {
                for (var i = 0; i < container.ObjectNodes.Length; i++)
                {
                    oldNodes[container.NodeNumber + (uint)i] = container.ObjectNodes[i];
                }

                return container.ObjectNodes.All(x => x.Data != 0);
            }

            var full = true;
            var childNodes = container.Nodes == LeafCapacity ? 1U : container.Nodes / (uint)IndexCapacity;
            for (var i = 0; i < container.Pointers.Length; i++)
            {
                var pointer = container.Pointers[i];
                if (pointer == 0)
                {
                    full = false;
                    continue;
                }

                var childBlock = pointer >> shiftsBlock32;
                if (!InVolume(childBlock) || !ClaimAdmin(childBlock))
                {
                    nodeTreeValid = false;
                    full = false;
                    AddIssue(IssueType.NodeContainerInvalid, block, null,
                        $"Node container pointer {i} to block {childBlock} is outside partition or used by another structure, object node tree is rebuilt");
                    continue;
                }

                nodeTreeBlocks.Add(childBlock);
                var childFull = await ReadNodeContainer(childBlock,
                    container.NodeNumber + (ulong)i * container.Nodes, childNodes, depth + 1);
                if (childFull == null)
                {
                    // invalid node container outside admin spaces, e.g. a data block, is not reused
                    if (!IsInAdminSpace(childBlock))
                    {
                        adminUsed.Remove(childBlock);
                        nodeTreeBlocks.Remove(childBlock);
                    }

                    full = false;
                    continue;
                }

                if (childFull.Value != ((pointer & 1) == 1))
                {
                    AddIssue(IssueType.NodeContainerInvalid, block, null,
                        $"Node container pointer {i} has full bit {pointer & 1}, but node container at block {childBlock} is {(childFull.Value ? "full" : "not full")}");
                }

                full &= childFull.Value;
            }

            return full;
        }

        /// <summary>
        /// Nodes of root node container is 1 for leaf or leaf capacity times a power of index capacity.
        /// </summary>
        private bool IsValidRootNodes(uint nodes)
        {
            if (nodes == 1)
            {
                return true;
            }

            if (nodes % LeafCapacity != 0)
            {
                return false;
            }

            nodes /= (uint)LeafCapacity;
            while (nodes > 1 && nodes % IndexCapacity == 0)
            {
                nodes /= (uint)IndexCapacity;
            }

            return nodes == 1;
        }

        /// <summary>
        /// Allocate new object nodes for objects with invalid or duplicate object node numbers.
        /// </summary>
        private void AllocateObjectNodes()
        {
            var objects = AllObjects().ToList();
            var used = new HashSet<uint>(objects.Where(x => !x.NeedsNewNode).Select(x => x.Object.ObjectNode));
            uint next = 7;
            foreach (var obj in objects.Where(x => x.NeedsNewNode))
            {
                while (used.Contains(next))
                {
                    next++;
                }

                obj.Object.ObjectNode = next;
                obj.Container.Dirty = true;
                used.Add(next);
                newNodes.Add(next);
                ShowMsg($"Object '{obj.Path}' got new object node {next}");
            }
        }

        /// <summary>
        /// Update object node tree to match objects. Object node tree is patched, if its structure is valid, otherwise
        /// it's rebuilt.
        /// </summary>
        private async Task FinalizeObjectNodes()
        {
            var desired = new SortedDictionary<uint, ObjectNode>();
            var objectsByNode = new Dictionary<uint, ObjModel>();
            foreach (var obj in AllObjects())
            {
                var node = obj.Object.ObjectNode;
                objectsByNode[node] = obj;
                if (obj.IsRootObject)
                {
                    oldNodes.TryGetValue(node, out var oldRoot);
                    desired[node] = new ObjectNode
                    {
                        Data = obj.Container.Block,
                        Next = oldRoot?.Next ?? 0,
                        Hash16 = oldRoot?.Hash16 ?? 0
                    };
                    continue;
                }

                desired[node] = new ObjectNode { Data = obj.Container.Block, Next = obj.HashNext, Hash16 = obj.Hash16 };
            }

            for (uint node = 2; node <= 6; node++)
            {
                if (!desired.ContainsKey(node))
                {
                    desired[node] = new ObjectNode { Data = Constants.ReservedNode };
                }
            }

            foreach (var entry in desired)
            {
                if (newNodes.Contains(entry.Key))
                {
                    continue;
                }

                objectsByNode.TryGetValue(entry.Key, out var obj);
                if (!oldNodes.TryGetValue(entry.Key, out var oldNode))
                {
                    if (nodeTreeValid)
                    {
                        AddIssue(IssueType.ObjectNodeInvalid, obj?.Container.Block ?? 0, obj?.Path,
                            $"Object node {entry.Key} doesn't exist in object node tree");
                    }

                    continue;
                }

                if (oldNode.Data != entry.Value.Data)
                {
                    AddIssue(IssueType.ObjectNodeInvalid, obj?.Container.Block ?? 0, obj?.Path,
                        entry.Value.Data == Constants.ReservedNode
                            ? $"Object node {entry.Key} points to block {oldNode.Data}, expected reserved"
                            : $"Object node {entry.Key} points to block {oldNode.Data}, expected object container {entry.Value.Data}");
                }
            }

            var notUsed = oldNodes
                .Where(x => x.Value.Data != 0 && !desired.ContainsKey(x.Key) && !removedNodes.Contains(x.Key))
                .Select(x => x.Key).ToList();
            if (notUsed.Count > 0)
            {
                AddIssue(IssueType.ObjectNodeNotUsed, rootBlock.ObjectNodeRoot, null,
                    $"{notUsed.Count} object nodes are in use, but not used by any object: {ListBlocks(notUsed)}",
                    true);
            }

            var leaves = nodeContainers.Where(x => x.IsLeaf).ToList();
            var covered = nodeTreeValid && desired.Keys.All(node =>
                leaves.Any(x => node >= x.NodeNumber && node < x.NodeNumber + (ulong)x.ObjectNodes.Length));

            if (covered)
            {
                PatchNodeTree(desired);
            }
            else
            {
                await RebuildNodeTree(desired);
            }
        }

        private void PatchNodeTree(IDictionary<uint, ObjectNode> desired)
        {
            var dirty = new HashSet<NodeContainer>();
            foreach (var leaf in nodeContainers.Where(x => x.IsLeaf))
            {
                for (var i = 0; i < leaf.ObjectNodes.Length; i++)
                {
                    var current = leaf.ObjectNodes[i];
                    desired.TryGetValue(leaf.NodeNumber + (uint)i, out var want);
                    want = want ?? new ObjectNode();
                    if (current.Data != want.Data || current.Next != want.Next || current.Hash16 != want.Hash16)
                    {
                        current.Data = want.Data;
                        current.Next = want.Next;
                        current.Hash16 = want.Hash16;
                        dirty.Add(leaf);
                    }
                }
            }

            var byBlock = nodeContainers.ToDictionary(x => x.OwnBlock);
            UpdateFullBits(byBlock[rootBlock.ObjectNodeRoot], byBlock, dirty);

            foreach (var container in dirty)
            {
                ChangeBlock(container.OwnBlock, NodeContainerBuilder.Build(container, volume.BlockSize));
            }
        }

        private bool UpdateFullBits(NodeContainer container, IDictionary<uint, NodeContainer> byBlock,
            ISet<NodeContainer> dirty)
        {
            if (container.IsLeaf)
            {
                return container.ObjectNodes.All(x => x.Data != 0);
            }

            var full = true;
            for (var i = 0; i < container.Pointers.Length; i++)
            {
                var pointer = container.Pointers[i];
                if (pointer == 0 || !byBlock.TryGetValue(pointer >> shiftsBlock32, out var child))
                {
                    full = false;
                    continue;
                }

                var childFull = UpdateFullBits(child, byBlock, dirty);
                var newPointer = (pointer & ~1U) | (childFull ? 1U : 0U);
                if (newPointer != pointer)
                {
                    container.Pointers[i] = newPointer;
                    dirty.Add(container);
                }

                full &= childFull;
            }

            return full;
        }

        /// <summary>
        /// Rebuild object node tree. Root node container stays at its block, blocks of old node containers are
        /// reused and new node containers are allocated from admin spaces.
        /// </summary>
        private async Task RebuildNodeTree(SortedDictionary<uint, ObjectNode> desired)
        {
            ShowMsg("Rebuilding object node tree");

            var root = rootBlock.ObjectNodeRoot;
            var pool = new Queue<uint>();
            foreach (var block in nodeTreeBlocks)
            {
                adminUsed.Remove(block);
                pool.Enqueue(block);
            }

            var keys = new SortedSet<uint>(desired.Keys);
            var maxNode = keys.Max;
            var leafCapacity = (uint)LeafCapacity;
            var indexCapacity = (uint)IndexCapacity;

            uint nodes = 1;
            if (maxNode >= 1 + leafCapacity)
            {
                nodes = leafCapacity;
                while ((ulong)nodes * indexCapacity < maxNode)
                {
                    nodes *= indexCapacity;
                }
            }

            BuildNodeContainer(root, 1, nodes, desired, keys, pool);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Build node container and its children.
        /// </summary>
        /// <returns>True, if node container is full.</returns>
        private bool BuildNodeContainer(uint block, uint nodeNumber, uint nodes,
            IDictionary<uint, ObjectNode> desired, SortedSet<uint> keys, Queue<uint> pool)
        {
            if (nodes == 1)
            {
                var leaf = NodeContainer.CreateLeaf(volume.BlockSize, nodeNumber);
                leaf.OwnBlock = block;
                for (var i = 0; i < leaf.ObjectNodes.Length; i++)
                {
                    if (desired.TryGetValue(nodeNumber + (uint)i, out var want))
                    {
                        leaf.ObjectNodes[i].Data = want.Data;
                        leaf.ObjectNodes[i].Next = want.Next;
                        leaf.ObjectNodes[i].Hash16 = want.Hash16;
                    }
                }

                ChangeBlock(block, NodeContainerBuilder.Build(leaf, volume.BlockSize));
                return leaf.ObjectNodes.All(x => x.Data != 0);
            }

            var index = NodeContainer.CreateIndex(volume.BlockSize, nodeNumber, nodes);
            index.OwnBlock = block;
            var childNodes = nodes == LeafCapacity ? 1U : nodes / (uint)IndexCapacity;
            var full = true;
            for (var i = 0; i < index.Pointers.Length; i++)
            {
                var start = nodeNumber + (ulong)i * nodes;
                var end = start + nodes - 1;
                if (start > keys.Max || keys.GetViewBetween((uint)start, (uint)System.Math.Min(end, uint.MaxValue)).Count == 0)
                {
                    full = false;
                    continue;
                }

                var childBlock = AllocateAdminBlock(pool);
                if (childBlock == 0)
                {
                    full = false;
                    continue;
                }

                var childFull = BuildNodeContainer(childBlock, (uint)start, childNodes, desired, keys, pool);
                index.Pointers[i] = (childBlock << shiftsBlock32) | (childFull ? 1U : 0U);
                full &= childFull;
            }

            ChangeBlock(block, NodeContainerBuilder.Build(index, volume.BlockSize));
            return full;
        }
    }
}
