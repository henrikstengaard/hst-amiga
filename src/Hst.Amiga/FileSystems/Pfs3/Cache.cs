﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using Blocks;

    public static class Cache
    {
        public static void LOCK(CachedBlock blk, globaldata g) => blk.used = g.locknr;
        public static void UNLOCKALL(globaldata g) => g.locknr++;
        public static bool ISLOCKED(CachedBlock blk, globaldata g) => blk.used == g.locknr;

        public static void ClearSearchInDirCache(uint dirnodenr, globaldata g)
        {
            if (!g.SearchInDirCache.ContainsKey(dirnodenr))
            {
                return;
            }

            g.SearchInDirCache.Remove(dirnodenr);
        }
    }
}