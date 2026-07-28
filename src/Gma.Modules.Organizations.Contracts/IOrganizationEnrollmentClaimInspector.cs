namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationEnrollmentClaimInspector
{
    Task<OrganizationEnrollmentClaimDto?> FindAsync(
        Guid organizationId,
        Guid enrollmentLinkId,
        string subjectId,
        CancellationToken cancellationToken = default);
}
