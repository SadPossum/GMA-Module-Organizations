namespace Gma.Modules.Organizations.Persistence;

public sealed class OrganizationsLifecycleOptions
{
    public const string SectionName = "Organizations:Lifecycle";

    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 100;
    public int MaxBatchesPerCategoryPerCycle { get; set; } = 4;
    public int IntervalMinutes { get; set; } = 5;
}
