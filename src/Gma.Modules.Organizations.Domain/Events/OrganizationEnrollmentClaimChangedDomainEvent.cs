namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationEnrollmentClaimChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid EnrollmentLinkId,
    Guid ClaimId,
    string SubjectId,
    OrganizationEnrollmentClaimChangeKind ChangeKind,
    OrganizationEnrollmentClaimState Status,
    Guid? MembershipId,
    long ClaimVersion) : DomainEvent(EventId, OccurredAtUtc);
