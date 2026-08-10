namespace Gma.Modules.Organizations.Contracts;

public interface IOrganizationAccessDecisionReader
{
    Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken);
}
