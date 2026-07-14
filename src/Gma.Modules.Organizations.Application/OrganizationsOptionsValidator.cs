namespace Gma.Modules.Organizations.Application;

using Microsoft.Extensions.Options;

internal sealed class OrganizationsOptionsValidator : IValidateOptions<OrganizationsOptions>
{
    private const int AbsoluteMaxInvitationLifetimeHours = 24 * 365;

    public ValidateOptionsResult Validate(string? name, OrganizationsOptions options)
    {
        List<string> failures = [];
        if (options.InvitationMaxLifetimeHours is < 1 or > AbsoluteMaxInvitationLifetimeHours)
        {
            failures.Add($"{OrganizationsOptions.SectionName}:InvitationMaxLifetimeHours must be between 1 and {AbsoluteMaxInvitationLifetimeHours}.");
        }

        if (options.InvitationDefaultLifetimeHours is < 1 ||
            options.InvitationDefaultLifetimeHours > options.InvitationMaxLifetimeHours)
        {
            failures.Add($"{OrganizationsOptions.SectionName}:InvitationDefaultLifetimeHours must be between 1 and InvitationMaxLifetimeHours.");
        }

        if (options.EnrollmentMaxLifetimeHours is < 1 or > AbsoluteMaxInvitationLifetimeHours ||
            options.EnrollmentDefaultLifetimeHours is < 1 ||
            options.EnrollmentDefaultLifetimeHours > options.EnrollmentMaxLifetimeHours)
        {
            failures.Add($"{OrganizationsOptions.SectionName}:Enrollment lifetime values are invalid.");
        }

        if (options.EnrollmentMaxClaims is < 1 or > 10_000)
        {
            failures.Add($"{OrganizationsOptions.SectionName}:EnrollmentMaxClaims must be between 1 and 10000.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
