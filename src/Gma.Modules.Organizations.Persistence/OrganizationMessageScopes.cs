namespace Gma.Modules.Organizations.Persistence;

internal static class OrganizationMessageScopes
{
    public static bool TryParse(string? scopeId, out Guid organizationId) =>
        Guid.TryParseExact(scopeId, "D", out organizationId) &&
        organizationId != Guid.Empty;
}
