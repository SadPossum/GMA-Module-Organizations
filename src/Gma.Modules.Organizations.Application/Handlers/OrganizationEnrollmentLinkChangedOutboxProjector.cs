namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationEnrollmentLinkChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentLinkChangedDomainEvent>
{
    public Task HandleAsync(OrganizationEnrollmentLinkChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentLinkChangedIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.EnrollmentLinkId,
                OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status),
                e.ReservedClaims,
                e.LinkVersion),
            cancellationToken);
}
