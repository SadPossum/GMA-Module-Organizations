namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Persistence.EntityFrameworkCore;

/// <summary>
/// Serializes organization creation and exact creation replay with irreversible
/// scope closure for one organization identity.
/// </summary>
internal static class OrganizationScopeExistenceTransactionLock
{
    // Existing creation paths already use this distributed lock protocol value.
    // Keep it stable so upgraded destruction workers coordinate with older nodes.
    private const string ResourcePrefix = "gma:organizations:create:";

    public static Task AcquireAsync(
        OrganizationsDbContext dbContext,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organization id is required for scope existence coordination.",
                nameof(organizationId));
        }

        return EfTransactionKeyLock.AcquireAsync(
            dbContext,
            $"{ResourcePrefix}{organizationId:N}",
            cancellationToken);
    }
}
