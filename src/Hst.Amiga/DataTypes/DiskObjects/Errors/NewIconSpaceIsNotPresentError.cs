namespace Hst.Amiga.DataTypes.DiskObjects.Errors
{
    using Core;

    public class NewIconSpaceIsNotPresentError : Error
    {
        public NewIconSpaceIsNotPresentError() : base(
            "Tool type with a space is not present before \"Don't edit the following lines\" tool type")
        {
        }
    }
}