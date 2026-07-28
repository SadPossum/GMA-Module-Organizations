namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.Permissions;
using Gma.Framework.Modules;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;

public static class OrganizationsModuleMetadata
{
    public const string Name = "organizations";
    public const string Schema = "organizations";
    public const string AdminSurfaceName = "admin";

    public static ModuleDescriptor Descriptor { get; } = ModuleDescriptor
        .Create(Name)
        .WithSchema(Schema)
        .WithAdminSurfaceName(AdminSurfaceName)
        .WithProfile(OrganizationsProfiles.Default)
        .WithPermissions([
            new ModulePermissionDescriptor(OrganizationsAdminPermissionCodes.Read, "Read organizations administration data.", scopeRequirement: PermissionScopeRequirement.Global),
            new ModulePermissionDescriptor(OrganizationsAdminPermissionCodes.Manage, "Manage organizations administration operations.", scopeRequirement: PermissionScopeRequirement.Global),
        ])
        .WithPublishedEvent<OrganizationChangedIntegrationEvent>()
        .WithPublishedEvent<OrganizationMembershipChangedIntegrationEvent>()
        .WithPublishedEvent<OrganizationInvitationChangedIntegrationEvent>()
        .WithPublishedEvent<OrganizationInvitationExpiredIntegrationEvent>()
        .WithPublishedEvent<OrganizationEnrollmentLinkChangedIntegrationEvent>()
        .WithPublishedEvent<OrganizationEnrollmentLinkExpiredIntegrationEvent>()
        .WithPublishedEvent<OrganizationEnrollmentClaimChangedIntegrationEvent>()
        .WithPublishedEvent<OrganizationEnrollmentClaimExpiredIntegrationEvent>()
        .Build();
}
