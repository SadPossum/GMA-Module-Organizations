namespace Gma.Modules.Organizations.Persistence;

public sealed class OrganizationsRetentionOptions
{
    public const string SectionName = "Organizations:Retention";

    public bool Enabled { get; set; }
    public int InvitationHistoryDays { get; set; } = 90;
    public int EnrollmentHistoryDays { get; set; } = 90;
    public int BatchSize { get; set; } = 500;
    public int MaxBatchesPerCategoryPerCycle { get; set; } = 4;
    public int IntervalMinutes { get; set; } = 60;
}
