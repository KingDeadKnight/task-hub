using System.Text.Json;

namespace TaskHub.Infrastructure.Sources.AzureDevOps;

// Wire contracts of the Azure DevOps REST API 7.1 (only the members we read).

internal sealed record WiqlRequest(string Query);

internal sealed record WiqlResponse(IReadOnlyList<WorkItemReference>? WorkItems);

internal sealed record WorkItemReference(int Id);

internal sealed record WorkItemsBatchRequest(IReadOnlyList<int> Ids, IReadOnlyList<string> Fields, string ErrorPolicy);

internal sealed record WorkItemsBatchResponse(IReadOnlyList<AdoWorkItem?>? Value);

public sealed record AdoWorkItem(int Id, IReadOnlyDictionary<string, JsonElement>? Fields);
