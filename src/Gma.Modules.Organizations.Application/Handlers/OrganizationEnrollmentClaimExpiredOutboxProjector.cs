namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationEnrollmentClaimExpiredOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentClaimExpiredDomainEvent>
{
    public Task HandleAsync(
        OrganizationEnrollmentClaimExpiredDomainEvent e,
        CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentClaimExpiredIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.EnrollmentLinkId,
                e.ClaimId,
                e.DecisionExpiresAtUtc,
                e.ClaimVersion),
            cancellationToken);
}
