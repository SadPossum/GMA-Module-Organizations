namespace Gma.Modules.Organizations.Application;

public sealed class OrganizationsOptions
{
    public const string SectionName = "Organizations";

    public bool SelfServiceCreationEnabled { get; set; }
    public int InvitationDefaultLifetimeHours { get; set; } = 168;
    public int InvitationMaxLifetimeHours { get; set; } = 720;
    public int EnrollmentDefaultLifetimeHours { get; set; } = 24;
    public int EnrollmentMaxLifetimeHours { get; set; } = 720;
    public int EnrollmentMaxClaims { get; set; } = 1_000;
}
