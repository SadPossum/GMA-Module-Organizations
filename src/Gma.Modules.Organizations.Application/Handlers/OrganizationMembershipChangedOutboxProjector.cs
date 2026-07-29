namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationMembershipChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationMembershipChangedDomainEvent>
{
    public Task HandleAsync(OrganizationMembershipChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationMembershipChangedIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.MembershipId,
                e.SubjectId,
                OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapRole(e.Role),
                OrganizationMappings.MapStatus(e.Status),
                e.MembershipVersion),
            cancellationToken);
}
