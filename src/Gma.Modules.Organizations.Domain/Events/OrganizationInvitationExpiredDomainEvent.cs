namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;

public sealed record OrganizationInvitationExpiredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    Guid InvitationId,
    DateTimeOffset ExpiresAtUtc,
    long InvitationVersion) : DomainEvent(EventId, OccurredAtUtc);
