namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationMembershipReader
{
    Task<OrganizationMembershipSnapshotDto?> FindAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken = default);
}
