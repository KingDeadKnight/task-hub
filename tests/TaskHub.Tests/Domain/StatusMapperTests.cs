using TaskHub.Domain;

namespace TaskHub.Tests.Domain;

public sealed class StatusMapperTests
{
    private readonly StatusMapper _mapper = new(new Dictionary<string, WorkItemStatus>
    {
        ["Active"] = WorkItemStatus.InProgress,
        ["Blocked"] = WorkItemStatus.Blocked,
    });

    [Theory]
    [InlineData("Active", WorkItemStatus.InProgress)]
    [InlineData("active", WorkItemStatus.InProgress)]
    [InlineData("  Blocked ", WorkItemStatus.Blocked)]
    public void Map_uses_configured_table_case_insensitively(string raw, WorkItemStatus expected)
    {
        _mapper.Map(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("Unknown state")]
    [InlineData("")]
    [InlineData(null)]
    public void Map_falls_back_for_unknown_status(string? raw)
    {
        _mapper.Map(raw).Should().Be(WorkItemStatus.Todo);
    }

    [Fact]
    public void Map_uses_custom_fallback()
    {
        var mapper = new StatusMapper(new Dictionary<string, WorkItemStatus>(), WorkItemStatus.InProgress);

        mapper.Map("Anything").Should().Be(WorkItemStatus.InProgress);
    }
}
