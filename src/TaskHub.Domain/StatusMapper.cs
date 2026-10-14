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

    public WorkItemStatus Map(string? rawStatus) =>
        rawStatus is not null && _mappings.TryGetValue(rawStatus.Trim(), out var status) ? status : _fallback;
}
