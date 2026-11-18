using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.Roms;
using Hst.Core;

namespace Hst.Amiga.ConsoleApp.Commands;

public class KickstartScsiCommand(string inputPath, string outputPath, bool extract, string keyFilePath = null)
    : CommandBase
{
    public override async Task<Result> Execute(CancellationToken token)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(inputPath, token);
            byte[] patched;
            if (extract)
            {
                var key = await KickstartCommand.ReadKey(inputPath, keyFilePath, token);
                patched = GetScsiCompatibility.Extract(KickstartRomReader.Read(bytes, key));
            }
            else
            {
                patched = await GetScsiCompatibility.Patch(bytes);
            }

            var destination = outputPath ?? (extract ? "scsi.device" : inputPath);
            if (!ReferenceEquals(bytes, patched) || outputPath != null)
            {
                await File.WriteAllBytesAsync(destination, patched, token);
            }

            OnInformationMessage(ReferenceEquals(bytes, patched) && outputPath == null
                ? $"'{destination}' already has the GetSCSI changes or does not need them."
                : $"Wrote GetSCSI-compatible scsi.device to '{destination}'");
            return new Result();
        }
        catch (IOException e)
        {
            return new Result(new Error(e.Message));
        }
        catch (InvalidDataException e)
        {
            return new Result(new Error(e.Message));
        }
    }
}
