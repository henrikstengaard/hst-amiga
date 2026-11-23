namespace Hst.Amiga.Vm;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Enables virtual terminal processing of escape sequences in Windows console output.
/// </summary>
internal static class WindowsConsole
{
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    /// <summary>
    /// Enable virtual terminal processing for standard output and error.
    /// </summary>
    /// <returns>Action restoring previous console modes.</returns>
    public static Action EnableVirtualTerminal()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return () => { };
        }

        var restoreOutput = Enable(StdOutputHandle);
        var restoreError = Enable(StdErrorHandle);
        return () =>
        {
            restoreOutput();
            restoreError();
        };
    }

    private static Action Enable(int stdHandle)
    {
        try
        {
            var handle = GetStdHandle(stdHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1) || !GetConsoleMode(handle, out var mode) ||
                (mode & EnableVirtualTerminalProcessing) != 0 ||
                !SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing))
            {
                return () => { };
            }

            return () => SetConsoleMode(handle, mode);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            return () => { };
        }
    }
}
