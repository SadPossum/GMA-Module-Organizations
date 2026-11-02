namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationMembershipInspector
{
    Task<OrganizationMembershipSnapshot?> FindAsync(
        Guid organizationId,
        Guid membershipId,
        string subjectId,
        CancellationToken cancellationToken = default);
}
