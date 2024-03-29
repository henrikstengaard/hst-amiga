﻿namespace Hst.Amiga.FileSystems.Pfs3
{
    using System.Collections.Generic;

    public class extrafields
    {
        /*
     * struct extrafields
{
	ULONG link;				// link anodenr						
    UWORD uid;				// user id							
    UWORD gid;				// group id							
    ULONG prot;				// byte 1-3 of protection			
    // rollover fields
    ULONG virtualsize;		// virtual rollover filesize in bytes (as shown by Examine()) 
    ULONG rollpointer;		// current start of file AND end of file pointer 
    // extended file size
    UWORD fsizex;           // extended bits 32-47 of direntry.fsize 
};

     */
        public extrafields(uint link, ushort uid, ushort gid, uint prot, uint virtualsize, uint rollpointer, ushort fsizex)
        {
            this.link = link;
            this.uid = uid;
            this.gid = gid;
            this.prot = prot & 0xffffff00; /* patch protection lower 8 bits */
            this.virtualsize = virtualsize;
            this.rollpointer = rollpointer;
            this.fsizex = fsizex;
            this.ExtraFieldsSize = CalculateSize(this);
        }

        public extrafields(extrafields extraFields) : this(extraFields.link, extraFields.uid, extraFields.gid,
            extraFields.prot, extraFields.virtualsize, extraFields.rollpointer, extraFields.fsizex)
        {
        }
        
        public extrafields() : this(0U, 0, 0, 0U, 0U, 0U, 0)
        {
        }

        /// <summary>
        /// link anodenr
        /// </summary>
        public uint link { get; private set; }

        /// <summary>
        /// user id
        /// </summary>
        public readonly ushort uid;

        /// <summary>
        /// group id
        /// </summary>
        public readonly ushort gid;

        /// <summary>
        /// byte 1-3 of protection
        /// </summary>
        public uint prot { get; private set; }

        /// <summary>
        /// virtual rollover filesize in bytes (as shown by Examine())
        /// </summary>
        public uint virtualsize { get; private set; }

        /// <summary>
        /// current start of file AND end of file pointer
        /// </summary>
        public uint rollpointer { get; private set; }

        /// <summary>
        /// extended bits 32-47 of direntry.fsize (large file support)
        /// </summary>
        public readonly ushort fsizex;

        public int ExtraFieldsSize { get; private set; }

        public void SetLink(uint link)
        {
            var updateSize = this.link != link; 
            this.link = link;
            if (updateSize)
            {
                this.ExtraFieldsSize = CalculateSize(this);                
            }
        }

        public void SetProtection(uint protection)
        {
            protection &= 0xffffff00; /* patch protection lower 8 bits */
            var updateSize = this.prot != protection; 
            this.prot = protection;
            if (updateSize)
            {
                this.ExtraFieldsSize = CalculateSize(this);                
            }
        }
        
        public void SetVirtualSize(uint virtualSize)
        {
            var updateSize = this.virtualsize != virtualSize; 
            this.virtualsize = virtualSize;
            if (updateSize)
            {
                this.ExtraFieldsSize = CalculateSize(this);                
            }
        }

        public void SetRollPointer(uint rollPointer)
        {
            var updateSize = this.rollpointer != rollPointer; 
            this.rollpointer = rollPointer;
            if (updateSize)
            {
                this.ExtraFieldsSize = CalculateSize(this);                
            }
        }
        
        private static int CalculateSize(extrafields extraFields)
        {
            // size of extra fields is 2, if all properties are 0
            if (extraFields.link == 0 && extraFields.uid == 0 && extraFields.gid == 0 && extraFields.prot == 0 &&
                extraFields.virtualsize == 0 && extraFields.rollpointer == 0 && extraFields.fsizex == 0)
            {
                return 2;
            }

            var fields = 0;
            var array = ConvertToUShortArray(extraFields);
            
            // increase j for each array element greater than 0            
            var j = 0;
            for (var i = 0; i < SizeOf.ExtraFields.Struct / 2; i++)
            {
                if (array[fields] != 0)
                {
                    fields++;
                    j++;
                }
                else
                {
                    fields++;
                }
            }

            // size of extra fields is number of ushorts greater than 0 (extra fields struct read as ushorts)
            // + 2 bytes for flags, which is the number of ushorts greater than 0
            // ... might be a way of packing data
            return 2 * j + 2;
        }
        
        /// <summary>
        /// Replicate c behavior of reading extra fields struct memory area as an array of ushorts: "UWORD *fields = (UWORD *)extra" and "array[j++] = *fields++"
        /// </summary>
        /// <param name="extraFields"></param>
        /// <returns></returns>
        public static ushort[] ConvertToUShortArray(extrafields extraFields)
        {
            var extraArray = new List<ushort>();

            // add link split into 2 ushorts
            extraArray.Add((ushort)(extraFields.link >> 16));
            extraArray.Add((ushort)(extraFields.link & 0xffff));

            extraArray.Add(extraFields.uid);
            extraArray.Add(extraFields.gid);
            
            // add prot split into 2 ushorts
            extraArray.Add((ushort)(extraFields.prot >> 16));
            extraArray.Add((ushort)(extraFields.prot & 0xffff));

            // add virtual size split into 2 ushorts
            extraArray.Add((ushort)(extraFields.virtualsize >> 16));
            extraArray.Add((ushort)(extraFields.virtualsize & 0xffff));
            
            // add roll pointer split into 2 ushorts
            extraArray.Add((ushort)(extraFields.rollpointer >> 16));
            extraArray.Add((ushort)(extraFields.rollpointer & 0xffff));

            extraArray.Add(extraFields.fsizex);

            return extraArray.ToArray();
        }
        
        /// <summary>
        /// Replicate c behavior of reading array of ushorts memory area as a extra fields struct. "UWORD *extra = (UWORD *)extrafields" and "*(extra++) = (flags & 1) ? *(--fields) : 0"
        /// </summary>
        /// <param name="extras"></param>
        /// <returns></returns>
        public static extrafields ConvertToExtraFields(ushort[] extras)
        {
            var link = ((uint)extras[0] << 16) | extras[1];
            var uid = extras[2];
            var gid = extras[3];
            var prot = ((uint)extras[4] << 16) | extras[5];
            var virtualSize = ((uint)extras[6] << 16) | extras[7];
            var rollpointer = ((uint)extras[8] << 16) | extras[9];
            var fsizex = extras[10];
            
            return new extrafields(link, uid, gid, prot, virtualSize, rollpointer, fsizex);
        }
    }
}