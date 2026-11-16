namespace Hst.Amiga.Vm.Libraries;

using Structs;

/// <summary>
/// Exec list operations on lists and nodes in memory.
/// </summary>
public static class ExecLists
{
    public static void NewList(Ram ram, uint list)
    {
        ram.WriteLong(list + ListStruct.Head, list + ListStruct.Tail);
        ram.WriteLong(list + ListStruct.Tail, 0);
        ram.WriteLong(list + ListStruct.TailPred, list);
    }

    public static bool IsEmpty(Ram ram, uint list) =>
        ram.ReadLong(list + ListStruct.TailPred) == list;

    public static void AddHead(Ram ram, uint list, uint node)
    {
        var head = ram.ReadLong(list + ListStruct.Head);
        ram.WriteLong(node + NodeStruct.Succ, head);
        ram.WriteLong(node + NodeStruct.Pred, list);
        ram.WriteLong(head + NodeStruct.Pred, node);
        ram.WriteLong(list + ListStruct.Head, node);
    }

    public static void AddTail(Ram ram, uint list, uint node)
    {
        var tailPred = ram.ReadLong(list + ListStruct.TailPred);
        ram.WriteLong(node + NodeStruct.Succ, list + ListStruct.Tail);
        ram.WriteLong(node + NodeStruct.Pred, tailPred);
        ram.WriteLong(tailPred + NodeStruct.Succ, node);
        ram.WriteLong(list + ListStruct.TailPred, node);
    }

    public static void Remove(Ram ram, uint node)
    {
        var succ = ram.ReadLong(node + NodeStruct.Succ);
        var pred = ram.ReadLong(node + NodeStruct.Pred);
        ram.WriteLong(pred + NodeStruct.Succ, succ);
        ram.WriteLong(succ + NodeStruct.Pred, pred);
    }

    public static uint RemHead(Ram ram, uint list)
    {
        var node = ram.ReadLong(list + ListStruct.Head);
        if (ram.ReadLong(node + NodeStruct.Succ) == 0)
        {
            return 0;
        }

        Remove(ram, node);
        return node;
    }

    public static uint RemTail(Ram ram, uint list)
    {
        var node = ram.ReadLong(list + ListStruct.TailPred);
        if (node == list)
        {
            return 0;
        }

        Remove(ram, node);
        return node;
    }

    /// <summary>
    /// Insert node after predecessor node or at head of list, if predecessor is zero.
    /// </summary>
    public static void Insert(Ram ram, uint list, uint node, uint pred)
    {
        if (pred == 0)
        {
            AddHead(ram, list, node);
            return;
        }

        var predSucc = ram.ReadLong(pred + NodeStruct.Succ);
        if (predSucc != 0)
        {
            ram.WriteLong(node + NodeStruct.Succ, predSucc);
            ram.WriteLong(node + NodeStruct.Pred, pred);
            ram.WriteLong(predSucc + NodeStruct.Pred, node);
            ram.WriteLong(pred + NodeStruct.Succ, node);
            return;
        }

        // predecessor is list tail, insert before it
        var tailPred = ram.ReadLong(pred + NodeStruct.Pred);
        ram.WriteLong(node + NodeStruct.Succ, pred);
        ram.WriteLong(node + NodeStruct.Pred, tailPred);
        ram.WriteLong(tailPred + NodeStruct.Succ, node);
        ram.WriteLong(pred + NodeStruct.Pred, node);
    }

    /// <summary>
    /// Insert node by priority before first node with lower priority.
    /// </summary>
    public static void Enqueue(Ram ram, uint list, uint node)
    {
        var priority = (sbyte)ram.ReadByte(node + NodeStruct.Pri);
        var current = ram.ReadLong(list + ListStruct.Head);
        uint succ;
        while ((succ = ram.ReadLong(current + NodeStruct.Succ)) != 0)
        {
            if ((sbyte)ram.ReadByte(current + NodeStruct.Pri) < priority)
            {
                break;
            }

            current = succ;
        }

        // insert before current
        var pred = ram.ReadLong(current + NodeStruct.Pred);
        ram.WriteLong(node + NodeStruct.Succ, current);
        ram.WriteLong(node + NodeStruct.Pred, pred);
        ram.WriteLong(pred + NodeStruct.Succ, node);
        ram.WriteLong(current + NodeStruct.Pred, node);
    }

    /// <summary>
    /// Find node with name, starting after start node or at head of list.
    /// </summary>
    public static uint FindName(Ram ram, uint start, string name)
    {
        var node = ram.ReadLong(start + NodeStruct.Succ);
        uint succ;
        while (node != 0 && (succ = ram.ReadLong(node + NodeStruct.Succ)) != 0)
        {
            var nodeName = ram.ReadCString(ram.ReadLong(node + NodeStruct.Name));
            if (nodeName == name)
            {
                return node;
            }

            node = succ;
        }

        return 0;
    }
}
