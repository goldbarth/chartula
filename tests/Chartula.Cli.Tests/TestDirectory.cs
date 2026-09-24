namespace Chartula.Cli.Tests;

internal static class TestDirectory
{
    /// <summary>
    /// Deletes a directory a test created, git repositories included.
    /// git writes its objects read-only, and on Windows a read-only file cannot be deleted.
    /// </summary>
    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
