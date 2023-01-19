﻿namespace Hst.Amiga.DataTypes.DiskObjects
{
    public class TextData
    {
        public uint Size { get; set; }
        public byte[] Data { get; set; }

        public TextData()
        {
            Size = 1;
            Data = new byte[]{ 0 };
        }
    }
}