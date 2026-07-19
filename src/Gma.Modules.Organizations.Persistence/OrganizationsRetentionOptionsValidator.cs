namespace Gma.Modules.Organizations.Persistence;

using Microsoft.Extensions.Options;

internal sealed class OrganizationsRetentionOptionsValidator
    : IValidateOptions<OrganizationsRetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, OrganizationsRetentionOptions options)
    {
        List<string> failures = [];

        if (options.InvitationHistoryDays is < 1 or > 3_650)
        {
            failures.Add("Organizations:Retention:InvitationHistoryDays must be between 1 and 3650.");
        }

        if (options.EnrollmentHistoryDays is < 1 or > 3_650)
        {
            failures.Add("Organizations:Retention:EnrollmentHistoryDays must be between 1 and 3650.");
        }

        if (options.BatchSize is < 1 or > 10_000)
        {
            failures.Add("Organizations:Retention:BatchSize must be between 1 and 10000.");
        }

        if (options.MaxBatchesPerCategoryPerCycle is < 1 or > 100)
        {
            failures.Add("Organizations:Retention:MaxBatchesPerCategoryPerCycle must be between 1 and 100.");
        }

        if (options.IntervalMinutes is < 1 or > 10_080)
        {
            failures.Add("Organizations:Retention:IntervalMinutes must be between 1 and 10080.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
