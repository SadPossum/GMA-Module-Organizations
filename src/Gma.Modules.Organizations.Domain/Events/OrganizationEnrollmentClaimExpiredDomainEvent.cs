namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationEnrollmentClaimExpiredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    Guid ClaimId,
    DateTimeOffset DecisionExpiresAtUtc,
    long ClaimVersion) : DomainEvent(EventId, OccurredAtUtc);
