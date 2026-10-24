namespace WinSticker.Tests;

internal static class TestPaths
{
    public static string FromRepository(string relativePath) =>
        Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "WinSticker", "WinSticker.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the WinSticker repository root.");
    }
}
