namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Application.Composition;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Application.Security;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
public static class DependencyInjection
{
    public static IServiceCollection AddOrganizationsApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(OrganizationsOptionsRegistrationMarker)))
        {
            services.AddSingleton<OrganizationsOptionsRegistrationMarker>();
            services
                .AddOptions<OrganizationsOptions>()
                .Bind(configuration.GetSection(OrganizationsOptions.SectionName))
                .ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<OrganizationsOptions>, OrganizationsOptionsValidator>());
        }

        services.TryAddScoped<IOrganizationAdmissionPolicy, DefaultOrganizationAdmissionPolicy>();
        services.TryAddScoped<IOrganizationInvitationAdmissionPolicy, DefaultOrganizationInvitationAdmissionPolicy>();
        services.TryAddScoped<OrganizationJoinAdmissionPolicy>();
        services.TryAddScoped<IOrganizationJoinTokenInspector, OrganizationJoinTokenInspector>();
        services.TryAddScoped<IOrganizationMembershipLifecycle, OrganizationMembershipLifecycle>();
        services.TryAddSingleton<IOrganizationInvitationTokenService, OrganizationInvitationTokenService>();
        services.TryAddSingleton<IOrganizationEnrollmentTokenService, OrganizationEnrollmentTokenService>();
        services.AddApplicationServicesFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }

    private sealed class OrganizationsOptionsRegistrationMarker;
}
