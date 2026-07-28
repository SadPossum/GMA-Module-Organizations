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

internal sealed class OrganizationEnrollmentLinkChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentLinkChangedDomainEvent>
{
    public Task HandleAsync(OrganizationEnrollmentLinkChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentLinkChangedIntegrationEvent(
                e.EventId, e.OccurredAtUtc, e.OrganizationId.ToString("D"), e.OrganizationId,
                e.EnrollmentLinkId, OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status), e.ReservedClaims, e.LinkVersion), cancellationToken);
}

internal sealed class OrganizationEnrollmentLinkExpiredOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentLinkExpiredDomainEvent>
{
    public Task HandleAsync(OrganizationEnrollmentLinkExpiredDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                e.EventId, e.OccurredAtUtc, e.OrganizationId.ToString("D"), e.OrganizationId,
                e.EnrollmentLinkId, e.ExpiresAtUtc, e.LinkVersion), cancellationToken);
}

internal sealed class OrganizationEnrollmentClaimChangedOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentClaimChangedDomainEvent>
{
    public Task HandleAsync(OrganizationEnrollmentClaimChangedDomainEvent e, CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentClaimChangedIntegrationEvent(
                e.EventId, e.OccurredAtUtc, e.OrganizationId.ToString("D"), e.OrganizationId,
                e.EnrollmentLinkId, e.ClaimId, e.SubjectId, OrganizationMappings.MapChange(e.ChangeKind),
                OrganizationMappings.MapStatus(e.Status), e.MembershipId, e.ClaimVersion), cancellationToken);
}

internal sealed class OrganizationEnrollmentClaimExpiredOutboxProjector(IOutboxWriterRegistry writers)
    : IDomainEventHandler<OrganizationEnrollmentClaimExpiredDomainEvent>
{
    public Task HandleAsync(
        OrganizationEnrollmentClaimExpiredDomainEvent e,
        CancellationToken cancellationToken) =>
        writers.GetRequired(OrganizationsModuleMetadata.Name).EnqueueAsync(
            new OrganizationEnrollmentClaimExpiredIntegrationEvent(
                e.EventId, e.OccurredAtUtc, e.OrganizationId.ToString("D"), e.OrganizationId,
                e.EnrollmentLinkId, e.ClaimId, e.DecisionExpiresAtUtc, e.ClaimVersion),
            cancellationToken);
}
