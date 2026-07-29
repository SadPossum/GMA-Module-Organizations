namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationInvitationChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationInvitationChangedDomainEvent>
{
    public Task HandleAsync(OrganizationInvitationChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationInvitationChangedIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.InvitationId,
                OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status),
                e.AcceptedSubjectId,
                e.InvitationVersion),
            cancellationToken);
}
