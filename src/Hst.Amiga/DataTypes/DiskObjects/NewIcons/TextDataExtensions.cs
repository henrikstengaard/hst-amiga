namespace Hst.Amiga.DataTypes.DiskObjects.NewIcons
{
    public static class TextDataExtensions
    {
        public static bool StartsWith(this TextData textData, byte[] bytes)
        {
            if (textData.Data.Length < bytes.Length)
            {
                return false;
            }

            for (var i = 0; i < bytes.Length; i++)
            {
                if (textData.Data[i] != bytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}