namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationInvitationExpiredOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationInvitationExpiredDomainEvent>
{
    public Task HandleAsync(OrganizationInvitationExpiredDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationInvitationExpiredIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.InvitationId,
                e.ExpiresAtUtc,
                e.InvitationVersion),
            cancellationToken);
}
