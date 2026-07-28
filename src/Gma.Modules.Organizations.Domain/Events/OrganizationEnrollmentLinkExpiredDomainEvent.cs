namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationEnrollmentLinkExpiredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    DateTimeOffset ExpiresAtUtc,
    long LinkVersion) : DomainEvent(EventId, OccurredAtUtc);
