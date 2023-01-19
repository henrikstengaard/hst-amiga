namespace Hst.Amiga.ConsoleApp.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using Core;
using Microsoft.Extensions.Logging;

public class IconCreateCommand : CommandBase
{
    private readonly ILogger<IconCreateCommand> logger;
    private readonly string path;
    private readonly IconType type;

    public IconCreateCommand(ILogger<IconCreateCommand> logger, string path, IconType type)
    {
        this.logger = logger;
        this.path = path;
        this.type = type;
    }

    public override Task<Result> Execute(CancellationToken token)
    {
        throw new NotImplementedException();
    }
}