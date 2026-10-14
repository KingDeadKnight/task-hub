namespace TaskHub.Infrastructure.Sources.AzureDevOps;

internal static class AzureDevOpsFields
{
    public const string Title = "System.Title";
    public const string State = "System.State";
    public const string TeamProject = "System.TeamProject";
    public const string ChangedDate = "System.ChangedDate";
    public const string Priority = "Microsoft.VSTS.Common.Priority";
    public const string DueDate = "Microsoft.VSTS.Scheduling.DueDate";
    public const string TargetDate = "Microsoft.VSTS.Scheduling.TargetDate";
    public const string Blocked = "Microsoft.VSTS.CMMI.Blocked";

    public static readonly IReadOnlyList<string> All =
        [Title, State, TeamProject, ChangedDate, Priority, DueDate, TargetDate, Blocked];
}
