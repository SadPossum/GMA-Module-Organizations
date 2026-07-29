namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationChangedDomainEvent>
{
    public Task HandleAsync(OrganizationChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationChangedIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status),
                e.OrganizationVersion),
            cancellationToken);
}
