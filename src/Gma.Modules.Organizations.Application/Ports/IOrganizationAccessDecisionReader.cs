namespace Gma.Modules.Organizations.Application.Ports;

public interface IOrganizationAccessDecisionReader
{
    Task<OrganizationAccessDecision> ReadAsync(
        Guid organizationId,
        string subjectId,
        CancellationToken cancellationToken);
}
