namespace Hst.Amiga.Extensions
{
    using System;
    using System.Collections.Generic;

    public static class ArrayExtensions
    {
        public static void ChunkBy<T>(this IEnumerable<T> source, int chunkSize, Action<IEnumerable<T>> chunkHandler)
        {
            var chunk = new List<T>();

            using (var enumerator = source.GetEnumerator())
            {
                for (; enumerator.MoveNext();)
                {
                    chunk.Add(enumerator.Current);

                    if (chunk.Count >= chunkSize)
                    {
                        chunkHandler(chunk);
                        chunk = new List<T>();
                    }
                }

                if (chunk.Count > 0)
                {
                    chunkHandler(chunk);
                }
            }
        }        
    }
}