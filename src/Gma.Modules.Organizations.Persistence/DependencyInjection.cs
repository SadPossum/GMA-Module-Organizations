namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Persistence.Access;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
public static class DependencyInjection
{
    public static IHostApplicationBuilder AddOrganizationsPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);
        OrganizationsRetentionOptions retentionOptions = builder.Configuration
            .GetSection(OrganizationsRetentionOptions.SectionName)
            .Get<OrganizationsRetentionOptions>() ?? new();
        ValidateOptionsResult retentionValidation = new OrganizationsRetentionOptionsValidator()
            .Validate(name: null, retentionOptions);
        if (retentionValidation.Failed)
        {
            throw new OptionsValidationException(
                OrganizationsRetentionOptions.SectionName,
                typeof(OrganizationsRetentionOptions),
                retentionValidation.Failures);
        }

        OrganizationsLifecycleOptions lifecycleOptions = builder.Configuration
            .GetSection(OrganizationsLifecycleOptions.SectionName)
            .Get<OrganizationsLifecycleOptions>() ?? new();
        ValidateOptionsResult lifecycleValidation = new OrganizationsLifecycleOptionsValidator()
            .Validate(name: null, lifecycleOptions);
        if (lifecycleValidation.Failed)
        {
            throw new OptionsValidationException(
                OrganizationsLifecycleOptions.SectionName,
                typeof(OrganizationsLifecycleOptions),
                lifecycleValidation.Failures);
        }

        builder.Services
            .AddOptions<OrganizationsRetentionOptions>()
            .Bind(builder.Configuration.GetSection(OrganizationsRetentionOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<OrganizationsRetentionOptions>,
            OrganizationsRetentionOptionsValidator>());
        builder.Services
            .AddOptions<OrganizationsLifecycleOptions>()
            .Bind(builder.Configuration.GetSection(OrganizationsLifecycleOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IValidateOptions<OrganizationsLifecycleOptions>,
            OrganizationsLifecycleOptionsValidator>());

        builder.Services.TryAddModuleDbContext<OrganizationsDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                OrganizationsMigrations.SqlServerAssembly,
                OrganizationsMigrations.PostgreSqlAssembly,
                OrganizationsMigrations.Schema,
                OrganizationsMigrations.HistoryTable));

        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IUnitOfWork, OrganizationsUnitOfWork>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxWriter, OrganizationsOutboxWriter>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxStore, OrganizationsOutboxStore>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IInboxStore, OrganizationsInboxStore>());
        builder.Services.TryAddScoped<IOrganizationAccessDecisionReader, OrganizationAccessDecisionReader>();
        builder.Services.TryAddScoped<IOrganizationAccessCandidateFilter, OrganizationAccessDecisionReader>();
        builder.Services.TryAddScoped<
            IOrganizationEnrollmentClaimInspector,
            OrganizationEnrollmentClaimInspector>();
        builder.Services.TryAddScoped<IOrganizationRepository, OrganizationRepository>();
        builder.Services.TryAddScoped<
            IOrganizationGovernanceCoordinator,
            OrganizationGovernanceCoordinator>();
        builder.Services.TryAddScoped<
            IOrganizationCreationCoordinator,
            OrganizationCreationCoordinator>();
        builder.Services.TryAddScoped<
            IOrganizationJoinSourceIssuanceCoordinator,
            OrganizationJoinSourceIssuanceCoordinator>();
        builder.Services.TryAddScoped<IOrganizationLifecycleRepository, OrganizationLifecycleRepository>();
        builder.Services.TryAddScoped<
            IOrganizationScopeLifecycle,
            OrganizationScopeLifecycleService>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(ICommandPipelineBehavior<,>),
            typeof(OrganizationsPersistenceRetryBehavior<,>)));
        builder.Services.MoveCommandUnitOfWorkBehaviorToEnd();

        if (retentionOptions.Enabled)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, OrganizationsRetentionService>());
        }

        if (lifecycleOptions.Enabled)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, OrganizationsLifecycleService>());
        }

        return builder;
    }
}
