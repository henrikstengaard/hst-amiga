namespace Hst.Amiga.Vm;

/// <summary>
/// Exception thrown when program calls a library function, which is not implemented.
/// </summary>
public class UnimplementedFunctionException : VmException
{
    public UnimplementedFunctionException(string libraryName, string functionName, int lvo)
        : base($"{libraryName}/{functionName} ({lvo}) is not implemented")
    {
        LibraryName = libraryName;
        FunctionName = functionName;
        Lvo = lvo;
    }

    public string LibraryName { get; }
    public string FunctionName { get; }
    public int Lvo { get; }
}
