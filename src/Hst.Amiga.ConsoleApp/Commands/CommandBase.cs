namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using Core;

public abstract class CommandBase
{
    public event EventHandler<string> DebugMessage;
    public event EventHandler<string> InformationMessage;

    protected virtual void OnDebugMessage(string message)
    {
        DebugMessage?.Invoke(this, message);
    }

    protected virtual void OnInformationMessage(string message)
    {
        InformationMessage?.Invoke(this, message);
    }
    
    public abstract Task<Result> Execute(CancellationToken token);
}