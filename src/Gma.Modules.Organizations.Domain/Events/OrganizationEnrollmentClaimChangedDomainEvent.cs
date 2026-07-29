namespace Gma.Modules.Organizations.Domain.Events;

using Gma.Framework.Domain;
using Gma.Modules.Organizations.Domain.Enums;

public sealed record OrganizationEnrollmentClaimChangedDomainEvent : DomainEvent
{
    public OrganizationEnrollmentClaimChangedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        Guid organizationId,
        Guid enrollmentLinkId,
        Guid claimId,
        string subjectId,
        OrganizationEnrollmentClaimChangeKind changeKind,
        OrganizationEnrollmentClaimState status,
        Guid? membershipId,
        long claimVersion)
        : base(eventId, occurredAtUtc)
    {
        this.OrganizationId = OrganizationDomainEventGuards.RequireId(
            organizationId,
            nameof(organizationId));
        this.EnrollmentLinkId = OrganizationDomainEventGuards.RequireId(
            enrollmentLinkId,
            nameof(enrollmentLinkId));
        this.ClaimId = OrganizationDomainEventGuards.RequireId(claimId, nameof(claimId));
        this.SubjectId = OrganizationDomainEventGuards.RequireSubjectId(subjectId, nameof(subjectId));
        this.ChangeKind = OrganizationDomainEventGuards.RequireKnown(changeKind, nameof(changeKind));
        this.Status = OrganizationDomainEventGuards.RequireKnown(status, nameof(status));
        this.MembershipId = OrganizationDomainEventGuards.RequireOptionalId(
            membershipId,
            nameof(membershipId));
        this.ClaimVersion = OrganizationDomainEventGuards.RequirePositiveVersion(
            claimVersion,
            nameof(claimVersion));
    }

    public Guid OrganizationId { get; }
    public Guid EnrollmentLinkId { get; }
    public Guid ClaimId { get; }
    public string SubjectId { get; }
    public OrganizationEnrollmentClaimChangeKind ChangeKind { get; }
    public OrganizationEnrollmentClaimState Status { get; }
    public Guid? MembershipId { get; }
    public long ClaimVersion { get; }
}
