namespace Hst.Amiga.Tests;

using System;

public static class BlockTestHelper
{
    public static byte[] CreateBlockBytes(int length)
    {
        var blockBytes = new byte[length];
        for (var i = 0; i < blockBytes.Length; i++)
        {
            if (i % 512 == 0)
            {
                var indexBytes = BitConverter.GetBytes(i / 512 + 1);
                Array.Copy(indexBytes, 0, blockBytes, i, indexBytes.Length);
                i += indexBytes.Length - 1;
                continue;
            }

            blockBytes[i] = (byte)(i % 256);
        }

        return blockBytes;
    }
}