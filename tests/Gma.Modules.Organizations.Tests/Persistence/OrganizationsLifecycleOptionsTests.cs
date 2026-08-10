namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsLifecycleOptionsTests
{
    [Fact]
    public void Scope_lifecycle_facade_is_registered_by_persistence()
    {
        HostApplicationBuilder builder = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] =
                "Server=localhost;Database=organizations-tests;Integrated Security=true;TrustServerCertificate=true"
        });

        builder.AddOrganizationsPersistence();

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IOrganizationScopeLifecycle) &&
            descriptor.ImplementationType ==
                typeof(OrganizationScopeLifecycleService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Lifecycle_is_disabled_by_default_with_valid_bounded_settings()
    {
        OrganizationsLifecycleOptions options = new();

        ValidateOptionsResult result = new OrganizationsLifecycleOptionsValidator()
            .Validate(name: null, options);

        Assert.False(options.Enabled);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(OrganizationsLifecycleOptions.BatchSize), 0)]
    [InlineData(nameof(OrganizationsLifecycleOptions.MaxBatchesPerCategoryPerCycle), 101)]
    [InlineData(nameof(OrganizationsLifecycleOptions.IntervalMinutes), 10_081)]
    public void Invalid_lifecycle_bounds_fail_startup_validation(string propertyName, int value)
    {
        OrganizationsLifecycleOptions options = new();
        typeof(OrganizationsLifecycleOptions).GetProperty(propertyName)!.SetValue(options, value);

        ValidateOptionsResult result = new OrganizationsLifecycleOptionsValidator()
            .Validate(name: null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Lifecycle_worker_registration_requires_explicit_enablement(
        bool enabled,
        bool expectedRegistration)
    {
        HostApplicationBuilder builder = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] =
                "Server=localhost;Database=organizations-tests;Integrated Security=true;TrustServerCertificate=true",
            [$"{OrganizationsLifecycleOptions.SectionName}:Enabled"] = enabled.ToString()
        });

        builder.AddOrganizationsPersistence();

        Assert.Equal(expectedRegistration, builder.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(OrganizationsLifecycleService)));
    }
}
