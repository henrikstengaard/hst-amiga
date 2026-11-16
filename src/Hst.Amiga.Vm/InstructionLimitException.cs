namespace Hst.Amiga.Vm;

/// <summary>
/// Exception thrown when max number of instructions is executed.
/// </summary>
public class InstructionLimitException : VmException
{
    public InstructionLimitException(long maxInstructions)
        : base($"Max number of instructions {maxInstructions} executed")
    {
    }
}
