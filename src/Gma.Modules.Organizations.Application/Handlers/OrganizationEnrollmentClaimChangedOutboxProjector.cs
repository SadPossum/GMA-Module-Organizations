namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Events;

internal sealed class OrganizationEnrollmentClaimChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentClaimChangedDomainEvent>
{
    public Task HandleAsync(
        OrganizationEnrollmentClaimChangedDomainEvent e,
        CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentClaimChangedIntegrationEvent(
                e.EventId,
                e.OccurredAtUtc,
                e.OrganizationId.ToString("D"),
                e.OrganizationId,
                e.EnrollmentLinkId,
                e.ClaimId,
                e.SubjectId,
                OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status),
                e.MembershipId,
                e.ClaimVersion),
            cancellationToken);
}
