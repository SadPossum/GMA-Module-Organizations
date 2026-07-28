namespace Gma.Modules.Organizations.Persistence;

using Microsoft.Extensions.Options;

internal sealed class OrganizationsLifecycleOptionsValidator
    : IValidateOptions<OrganizationsLifecycleOptions>
{
    public ValidateOptionsResult Validate(string? name, OrganizationsLifecycleOptions options)
    {
        List<string> failures = [];

        if (options.BatchSize is < 1 or > 10_000)
        {
            failures.Add("Organizations:Lifecycle:BatchSize must be between 1 and 10000.");
        }

        if (options.MaxBatchesPerCategoryPerCycle is < 1 or > 100)
        {
            failures.Add(
                "Organizations:Lifecycle:MaxBatchesPerCategoryPerCycle must be between 1 and 100.");
        }

        if (options.IntervalMinutes is < 1 or > 10_080)
        {
            failures.Add("Organizations:Lifecycle:IntervalMinutes must be between 1 and 10080.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
