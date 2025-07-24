namespace Hst.Amiga.ConsoleApp.Extensions;

public static class StringExtensions
{
    public static string AddCenteredText(this string line, string text)
    {
        var positionX = (line.Length - text.Length) / 2;
        return string.Concat(
            line.Substring(0, positionX),
            text,
            line.Substring(line.Length - positionX, positionX));
    }
}