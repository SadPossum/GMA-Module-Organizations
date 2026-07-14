namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationMembershipChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid MembershipId,
    string SubjectId,
    OrganizationMembershipChangeKind ChangeKind,
    OrganizationMembershipRole Role,
    OrganizationMembershipState Status,
    long MembershipVersion) : DomainEvent(EventId, OccurredAtUtc);
