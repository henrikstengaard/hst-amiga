namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class IconTag
    {
        public readonly Constants.IconAttributeTags Tag;
        public readonly uint Value;
        
        public IconTag(Constants.IconAttributeTags tag, uint value)
        {
            Tag = tag;
            Value = value;
        }
    }
}