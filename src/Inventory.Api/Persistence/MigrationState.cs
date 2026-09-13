namespace Inventory.Api.Persistence;

internal enum MigrationStatus
{
    Pending,
    Running,
    Applied,
    Failed,
}

/// <summary>
/// Where the startup migration got to, published as a singleton so a health check
/// can report it without knowing anything about the migrator.
/// </summary>
internal sealed class MigrationState
{
    private readonly Lock _gate = new();
    private MigrationStatus _status = MigrationStatus.Pending;
    private string? _error;

    public (MigrationStatus Status, string? Error) Read()
    {
        lock (_gate)
        {
            return (_status, _error);
        }
    }

    public void MarkRunning() => Set(MigrationStatus.Running, error: null);

    public void MarkApplied() => Set(MigrationStatus.Applied, error: null);

    public void MarkFailed(string error) => Set(MigrationStatus.Failed, error);

    private void Set(MigrationStatus status, string? error)
    {
        lock (_gate)
        {
            _status = status;
            _error = error;
        }
    }
}
