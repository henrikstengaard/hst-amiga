﻿namespace Hst.Amiga.DataTypes.DiskObjects
{
    using System.Collections.Generic;

    public class ToolTypes
    {
        public IEnumerable<TextData> TextDatas { get; set; }

        public ToolTypes()
        {
            TextDatas = new List<TextData>();
        }
    }
}