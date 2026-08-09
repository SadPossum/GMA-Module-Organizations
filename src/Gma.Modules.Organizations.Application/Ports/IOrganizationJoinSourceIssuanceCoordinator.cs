namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Modules.Organizations.Domain.Aggregates;

public interface IOrganizationJoinSourceIssuanceCoordinator
{
    Task<OrganizationInvitation?> AcquireInvitationAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken);

    Task<OrganizationEnrollmentLink?> AcquireEnrollmentLinkAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken);
}
