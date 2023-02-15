namespace Hst.Amiga.DataTypes.DiskObjects.ColorIcons
{
    using System;
    using System.IO;

    public class RleStreamWriter
    {
        private readonly Stream stream;
        private readonly int bitDepth;
        private const int MaxRepeat = 0x7f;
        private int bitsLeft;
        private byte nextByte;

        private readonly byte[] buffer;
        private int size;
        private RleMode mode;

        private enum RleMode
        {
            Copy,
            Repeat
        }

        public RleStreamWriter(Stream stream, int bitDepth)
        {
            this.stream = stream;
            this.bitDepth = bitDepth;
            bitsLeft = 8;

            this.buffer = new byte[MaxRepeat];
            this.size = 0;
            this.mode = RleMode.Copy;
        }

        private bool Flush(byte data)
        {
            switch (size)
            {
                case 0:
                    return false;
                case MaxRepeat:
                    return true;
            }

            var repeated = buffer[size - 1] == data;
            
            switch (this.mode)
            {
                case RleMode.Copy:
                    if (!repeated || size != 1)
                    {
                        return repeated;
                    }
                    // switch to repeat mode, if size is 1 and previous is equal 
                    this.mode = RleMode.Repeat;
                    return false;
                case RleMode.Repeat:
                    return !repeated;
            }

            return false;
        }

        public void Write(byte data)
        {
            if (Flush(data))
            {
                WriteRleBlock();
            }
            
            buffer[size++] = data;
        }
        
        public void Finish()
        {
            WriteRleBlock();
            stream.WriteByte(nextByte);
        }

        private void WriteRleBlock()
        {
            if (this.size == 0)
            {
                throw new Exception("Rle buffer is empty");
            }

            switch (this.mode)
            {
                case RleMode.Copy:
                    BitsAdd(8, this.size - 1);
                    for (var i = 0; i < this.size; i++)
                    {
                        BitsAdd(bitDepth, this.buffer[i]);
                    }
                    break;
                case RleMode.Repeat:
                    BitsAdd(8, 256 - this.size + 1);
                    BitsAdd(bitDepth, this.buffer[0]);
                    break;
            }

            this.size = 0;
            this.buffer[0] = 0;
            this.mode = RleMode.Copy;
        }
        
        private byte BitsAdd(int n, int bits)
        {
            if (bitsLeft == 0)
            {
                stream.WriteByte(nextByte);
                nextByte = 0;
                bitsLeft = 8;
            }

            bitsLeft -= n;
            if (bitsLeft >= 0)
            {
                nextByte = (byte)(nextByte | ((bits << bitsLeft) & 0xff));
            }
            else
            {
                nextByte = (byte)(nextByte | (bits >> -bitsLeft));
                stream.WriteByte(nextByte);
                nextByte = 0;
                bitsLeft += 8;
                nextByte = (byte)(nextByte | ((bits << bitsLeft) & 0xff));
            }

            return nextByte;
        }
    }
}