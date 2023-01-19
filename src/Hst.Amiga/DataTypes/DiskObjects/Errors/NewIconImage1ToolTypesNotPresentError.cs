namespace Hst.Amiga.DataTypes.DiskObjects.Errors
{
    using Core;

    public class NewIconImage1ToolTypesNotPresentError : Error
    {
        public NewIconImage1ToolTypesNotPresentError() : base(
            "NewIcon image 1 tool types are not present after \"Don't edit the following lines\" tool type")
        {
        }
    }
}