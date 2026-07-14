namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationInvitationChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid InvitationId,
    OrganizationInvitationChangeKind ChangeKind,
    OrganizationInvitationState Status,
    string? AcceptedSubjectId,
    long InvitationVersion) : DomainEvent(EventId, OccurredAtUtc);
