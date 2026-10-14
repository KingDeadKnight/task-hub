using TaskHub.Domain;

namespace TaskHub.Tests.Domain;

public sealed class WorkItemTests
{
    [Fact]
    public void BuildId_combines_source_and_external_id()
    {
        WorkItem.BuildId("azuredevops", "1234").Should().Be("azuredevops:1234");
    }
}
