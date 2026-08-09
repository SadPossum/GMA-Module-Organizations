namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationEnrollmentClaimWithdrawnOutboxProjector(
    IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentClaimWithdrawnDomainEvent>
{
    public Task HandleAsync(
        OrganizationEnrollmentClaimWithdrawnDomainEvent e,
        CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentClaimWithdrawnIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.EnrollmentLinkId,
                e.ClaimId,
                e.ClaimVersion),
            cancellationToken);
}
