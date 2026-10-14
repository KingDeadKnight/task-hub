namespace TaskHub.Domain;

/// <summary>Converts a source's native status into a <see cref="WorkItemStatus"/> using a configurable table.</summary>
public sealed class StatusMapper
{
    private readonly Dictionary<string, WorkItemStatus> _mappings;
    private readonly WorkItemStatus _fallback;

    public StatusMapper(IReadOnlyDictionary<string, WorkItemStatus> mappings, WorkItemStatus fallback = WorkItemStatus.Todo)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        _mappings = new Dictionary<string, WorkItemStatus>(mappings, StringComparer.OrdinalIgnoreCase);
        _fallback = fallback;
    }

    public WorkItemStatus Map(string? rawStatus) => TryMap(rawStatus, out var status) ? status : _fallback;

    /// <summary>Looks the status up in the table only, so callers can apply their own fallback.</summary>
    public bool TryMap(string? rawStatus, out WorkItemStatus status)
    {
        status = _fallback;
        return rawStatus is not null && _mappings.TryGetValue(rawStatus.Trim(), out status);
    }
}
