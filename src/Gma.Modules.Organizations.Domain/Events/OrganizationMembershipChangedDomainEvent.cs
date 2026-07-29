namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationMembershipChangedDomainEvent : DomainEvent
{
    public OrganizationMembershipChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid membershipId,
        string subjectId,
        OrganizationMembershipChangeKind changeKind,
        OrganizationMembershipRole role,
        OrganizationMembershipState status,
        long membershipVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.MembershipId = OrganizationDomainEventGuards.RequireId(membershipId, nameof(membershipId));
        this.SubjectId = OrganizationDomainEventGuards.RequireSubjectId(subjectId, nameof(subjectId));
        this.ChangeKind = OrganizationDomainEventGuards.RequireKnown(changeKind, nameof(changeKind));
        this.Role = OrganizationDomainEventGuards.RequireKnown(role, nameof(role));
        this.Status = OrganizationDomainEventGuards.RequireKnown(status, nameof(status));
        this.MembershipVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            membershipVersion,
            nameof(membershipVersion));
    }

    public Guid OrganizationId { get; }
    public Guid MembershipId { get; }
    public string SubjectId { get; }
    public OrganizationMembershipChangeKind ChangeKind { get; }
    public OrganizationMembershipRole Role { get; }
    public OrganizationMembershipState Status { get; }
    public long MembershipVersion { get; }
}
