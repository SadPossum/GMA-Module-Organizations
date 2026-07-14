namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationEnrollmentLinkChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    OrganizationEnrollmentLinkChangeKind ChangeKind,
    OrganizationEnrollmentLinkState Status,
    int ReservedClaims,
    long LinkVersion) : DomainEvent(EventId, OccurredAtUtc);
