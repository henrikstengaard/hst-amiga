﻿namespace Hst.Amiga.FileSystems.FastFileSystem
{
    public class NameToEntryBlockResult
    {
        public int NSect { get; set; }
        public EntryBlock EntryBlock { get; set; }
        public int? NUpdSect { get; set; }
    }
}