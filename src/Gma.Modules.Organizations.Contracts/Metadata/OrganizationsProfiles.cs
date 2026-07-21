namespace Gma.Modules.Organizations.Contracts;

using Gma.Framework.ModuleComposition;

public static class OrganizationsProfiles
{
    public const string DefaultName = "default";

    public static ModuleProfileDescriptor Default { get; } = new(
        OrganizationsModuleMetadata.Name,
        DefaultName,
        displayName: "Organizations default",
        description: "Organization tenancy, membership governance, invitations, and enrollment.");
}
