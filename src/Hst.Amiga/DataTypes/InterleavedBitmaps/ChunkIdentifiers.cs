namespace Hst.Amiga.DataTypes.InterleavedBitmaps
{
    public static class ChunkIdentifiers
    {
        /// <summary>
        /// Bitmap header identifier indicates chunk contains image dimensions, encoding used and other data needed to read body chunk.
        /// </summary>
        public const string BitmapHeader = "BMHD";
        
        /// <summary>
        /// Color map identifier indicates chunk contains color map or palette.
        /// </summary>
        public const string ColorMap = "CMAP";
        
        /// <summary>
        /// Commodore Amiga chunk identifier indicates chunk contains view mode data specific for Amiga computers.
        /// </summary>
        public const string Camg = "CAMG";
        
        /// <summary>
        /// Interleaved bitmap identifier indicating chunk contains an interleaved bitmap.
        /// </summary>
        public const string InterleavedBitmap = "ILBM";
        
        /// <summary>
        /// Form identifier indicating chunk contains a file format
        /// </summary>
        public const string Form = "FORM";
        
        /// <summary>
        /// Body identifier indicates chunk contains all bit planes and optional mask, interleaved by row. 
        /// </summary>
        public const string Body = "BODY";
    }
}