namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationEnrollmentLinkExpiredOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentLinkExpiredDomainEvent>
{
    public Task HandleAsync(OrganizationEnrollmentLinkExpiredDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.EnrollmentLinkId,
                e.ExpiresAtUtc,
                e.LinkVersion),
            cancellationToken);
}
