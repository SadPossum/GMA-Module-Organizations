namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationScopeLifecycle
{
    Task<OrganizationScopeSnapshot> GetSnapshotAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<OrganizationScopeExportPage> ExportAsync(
        OrganizationScopeExportRequest request,
        CancellationToken cancellationToken);

    Task<OrganizationScopeDestroyResult> DestroyBatchAsync(
        OrganizationScopeDestroyRequest request,
        CancellationToken cancellationToken);
}
