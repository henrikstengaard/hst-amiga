﻿namespace Hst.Amiga.DataTypes.DiskObjects.Errors
{
    using Core;

    public class NewIconImage2ToolTypesNotPresentError : Error
    {
        public NewIconImage2ToolTypesNotPresentError() : base(
            "NewIcon image 2 tool types are not present after last NewIcon image number 1 tool type")
        {
        }
    }
}