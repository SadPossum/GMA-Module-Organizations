namespace Gma.Modules.Organizations.Tests;

using Gma.Modules.Organizations.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsRetentionOptionsTests
{
    [Fact]
    public void Retention_is_disabled_by_default_with_valid_bounded_settings()
    {
        OrganizationsRetentionOptions options = new();

        ValidateOptionsResult result = new OrganizationsRetentionOptionsValidator()
            .Validate(name: null, options);

        Assert.False(options.Enabled);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(OrganizationsRetentionOptions.InvitationHistoryDays), 0)]
    [InlineData(nameof(OrganizationsRetentionOptions.EnrollmentHistoryDays), 3_651)]
    [InlineData(nameof(OrganizationsRetentionOptions.BatchSize), 0)]
    [InlineData(nameof(OrganizationsRetentionOptions.MaxBatchesPerCategoryPerCycle), 101)]
    [InlineData(nameof(OrganizationsRetentionOptions.IntervalMinutes), 10_081)]
    public void Invalid_retention_bounds_fail_startup_validation(string propertyName, int value)
    {
        OrganizationsRetentionOptions options = new();
        typeof(OrganizationsRetentionOptions).GetProperty(propertyName)!.SetValue(options, value);

        ValidateOptionsResult result = new OrganizationsRetentionOptionsValidator()
            .Validate(name: null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Retention_worker_registration_requires_explicit_enablement(
        bool enabled,
        bool expectedRegistration)
    {
        HostApplicationBuilder builder = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = "Server=localhost;Database=organizations-tests;Integrated Security=true;TrustServerCertificate=true",
            [$"{OrganizationsRetentionOptions.SectionName}:Enabled"] = enabled.ToString()
        });

        builder.AddOrganizationsPersistence();

        Assert.Equal(expectedRegistration, builder.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(OrganizationsRetentionService)));
    }
}
