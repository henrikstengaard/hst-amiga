namespace Hst.Amiga.Tests;

public static class TestHelper
{
    public static void DeletePaths(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
            else if (System.IO.Directory.Exists(path))
            {
                System.IO.Directory.Delete(path, true);
            }
        }
    }
}