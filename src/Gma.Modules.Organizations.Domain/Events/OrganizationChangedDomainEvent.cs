namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationChangedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    Guid OrganizationId,
    OrganizationChangeKind ChangeKind,
    OrganizationState Status,
    long OrganizationVersion) : DomainEvent(EventId, OccurredAtUtc);
