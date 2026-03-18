namespace Hst.Amiga.DataTypes.DiskObjects.TrueColorIcons
{
    public class IconAttributeTag
    {
        public readonly Constants.IconAttributeTags Tag;
        public readonly uint Value;
        
        public IconAttributeTag(Constants.IconAttributeTags tag, uint value)
        {
            Tag = tag;
            Value = value;
        }
    }
}