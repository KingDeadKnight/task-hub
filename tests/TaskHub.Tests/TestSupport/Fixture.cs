namespace TaskHub.Tests.TestSupport;

internal static class Fixture
{
    public static string Read(params string[] relativePath) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "Fixtures", .. relativePath]));
}
