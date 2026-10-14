using Microsoft.Data.Sqlite;

namespace TaskHub.Tests.TestSupport;

/// <summary>
/// A throwaway SQLite file. A file (rather than :memory:) lets several DbContexts use it concurrently,
/// as the sync does with one context per source.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"taskhub-test-{Guid.NewGuid():N}.db");
    private bool _disposed;

    public string ConnectionString => $"Data Source={_path}";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            File.Delete(file);
        }
    }
}
