namespace Hst.Amiga.DataTypes.DiskObjects.Errors
{
    using Core;

    public class NewIconHeaderIsNotPresentError : Error
    {
        public NewIconHeaderIsNotPresentError() : base(
            "New icon header \"Don't edit the following lines\" is not present in tool types")
        {
        }
    }
}