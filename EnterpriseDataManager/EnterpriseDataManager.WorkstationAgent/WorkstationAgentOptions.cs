namespace EnterpriseDataManager.WorkstationAgent;

public sealed class WorkstationAgentOptions
{
    public const string SectionName = "WorkstationAgent";

    public string ApiBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Schedule { get; set; } = "0 * * * *";
    public string SourcePath { get; set; } = string.Empty;
    public string Label { get; set; } = "Workstation Backup";
    public string Description { get; set; } = "Automated workstation agent backup";
}
