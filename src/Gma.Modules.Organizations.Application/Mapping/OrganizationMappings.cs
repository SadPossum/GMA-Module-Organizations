namespace Gma.Modules.Organizations.Application.Mapping;

using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using DomainMembershipState = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipState;
using DomainOrganizationState = Gma.Modules.Organizations.Domain.Enums.OrganizationState;
using ContractMembershipRole = Gma.Modules.Organizations.Contracts.OrganizationMembershipRole;
using DomainOrganizationChange = Gma.Modules.Organizations.Domain.Enums.OrganizationChangeKind;
using DomainMembershipChange = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipChangeKind;
using DomainInvitationState = Gma.Modules.Organizations.Domain.Enums.OrganizationInvitationState;
using DomainInvitationChange = Gma.Modules.Organizations.Domain.Enums.OrganizationInvitationChangeKind;
using DomainEnrollmentMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;
using DomainEnrollmentLinkState = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentLinkState;
using DomainEnrollmentLinkChange = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentLinkChangeKind;
using DomainEnrollmentClaimState = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentClaimState;
using DomainEnrollmentClaimChange = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentClaimChangeKind;

public static class OrganizationMappings
{
    public static OrganizationDto ToDto(this Organization organization) => new(
        organization.Id,
        organization.ScopeId,
        organization.Name,
        organization.Slug,
        MapStatus(organization.Status),
        organization.ActiveOwnerCount,
        organization.Version,
        organization.CreatedAtUtc,
        organization.LastChangedAtUtc);

    public static OrganizationMembershipDto ToDto(this OrganizationMembership membership) => new(
        membership.Id,
        membership.OrganizationId,
        membership.SubjectId,
        MapRole(membership.Role),
        MapStatus(membership.Status),
        membership.Version,
        membership.JoinedAtUtc,
        membership.LastChangedAtUtc);

    public static OrganizationInvitationDto ToDto(
        this OrganizationInvitation invitation,
        DateTimeOffset nowUtc) => new(
        invitation.Id,
        invitation.OrganizationId,
        invitation.InviterSubjectId,
        invitation.RecipientEmail,
        invitation.ExpiresAtUtc,
        MapStatus(invitation.Status, invitation.ExpiresAtUtc, nowUtc),
        invitation.AcceptedSubjectId,
        invitation.AcceptedMembershipId,
        invitation.Version,
        invitation.CreatedAtUtc,
        invitation.LastChangedAtUtc);

    public static OrganizationEnrollmentLinkDto ToDto(
        this OrganizationEnrollmentLink link,
        DateTimeOffset nowUtc) => new(
        link.Id, link.OrganizationId, link.CreatorSubjectId, link.ExpiresAtUtc,
        link.MaximumClaims, link.ReservedClaims, MapMode(link.ApprovalMode),
        MapStatus(link.Status, link.ExpiresAtUtc, link.ReservedClaims, link.MaximumClaims, nowUtc),
        link.Version, link.CreatedAtUtc, link.LastChangedAtUtc);

    public static OrganizationEnrollmentClaimDto ToDto(this OrganizationEnrollmentClaim claim) => new(
        claim.Id, claim.EnrollmentLinkId, claim.OrganizationId, claim.SubjectId,
        MapStatus(claim.Status), claim.MembershipId, claim.Version,
        claim.CreatedAtUtc, claim.LastChangedAtUtc);

    public static OrganizationStatus MapStatus(DomainOrganizationState status) => status switch
    {
        DomainOrganizationState.Active => OrganizationStatus.Active,
        DomainOrganizationState.Suspended => OrganizationStatus.Suspended,
        DomainOrganizationState.Archived => OrganizationStatus.Archived,
        _ => OrganizationStatus.Unknown
    };

    public static ContractMembershipRole MapRole(DomainMembershipRole role) => role switch
    {
        DomainMembershipRole.Member => ContractMembershipRole.Member,
        DomainMembershipRole.Owner => ContractMembershipRole.Owner,
        _ => ContractMembershipRole.Unknown
    };

    public static OrganizationMembershipStatus MapStatus(DomainMembershipState status) => status switch
    {
        DomainMembershipState.Active => OrganizationMembershipStatus.Active,
        DomainMembershipState.Suspended => OrganizationMembershipStatus.Suspended,
        DomainMembershipState.Removed => OrganizationMembershipStatus.Removed,
        _ => OrganizationMembershipStatus.Unknown
    };

    public static OrganizationChange MapChange(DomainOrganizationChange change) => change switch
    {
        DomainOrganizationChange.Created => OrganizationChange.Created,
        DomainOrganizationChange.ProfileUpdated => OrganizationChange.ProfileUpdated,
        DomainOrganizationChange.Suspended => OrganizationChange.Suspended,
        DomainOrganizationChange.Reactivated => OrganizationChange.Reactivated,
        DomainOrganizationChange.Archived => OrganizationChange.Archived,
        DomainOrganizationChange.OwnerCountChanged => OrganizationChange.OwnerCountChanged,
        DomainOrganizationChange.OwnershipTransferred => OrganizationChange.OwnershipTransferred,
        _ => OrganizationChange.Unknown
    };

    public static OrganizationMembershipChange MapChange(DomainMembershipChange change) => change switch
    {
        DomainMembershipChange.Joined => OrganizationMembershipChange.Joined,
        DomainMembershipChange.Suspended => OrganizationMembershipChange.Suspended,
        DomainMembershipChange.Resumed => OrganizationMembershipChange.Resumed,
        DomainMembershipChange.Removed => OrganizationMembershipChange.Removed,
        DomainMembershipChange.PromotedToOwner => OrganizationMembershipChange.PromotedToOwner,
        DomainMembershipChange.DemotedToMember => OrganizationMembershipChange.DemotedToMember,
        DomainMembershipChange.Restored => OrganizationMembershipChange.Restored,
        _ => OrganizationMembershipChange.Unknown
    };

    public static OrganizationInvitationStatus MapStatus(
        DomainInvitationState status,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset nowUtc) => status == DomainInvitationState.Pending && expiresAtUtc <= nowUtc
            ? OrganizationInvitationStatus.Expired
            : status switch
            {
                DomainInvitationState.Pending => OrganizationInvitationStatus.Pending,
                DomainInvitationState.Accepted => OrganizationInvitationStatus.Accepted,
                DomainInvitationState.Revoked => OrganizationInvitationStatus.Revoked,
                DomainInvitationState.Superseded => OrganizationInvitationStatus.Superseded,
                _ => OrganizationInvitationStatus.Unknown
            };

    public static OrganizationInvitationStatus MapStatus(DomainInvitationState status) => status switch
    {
        DomainInvitationState.Pending => OrganizationInvitationStatus.Pending,
        DomainInvitationState.Accepted => OrganizationInvitationStatus.Accepted,
        DomainInvitationState.Revoked => OrganizationInvitationStatus.Revoked,
        DomainInvitationState.Superseded => OrganizationInvitationStatus.Superseded,
        _ => OrganizationInvitationStatus.Unknown
    };

    public static OrganizationInvitationChange MapChange(DomainInvitationChange change) => change switch
    {
        DomainInvitationChange.Created => OrganizationInvitationChange.Created,
        DomainInvitationChange.Accepted => OrganizationInvitationChange.Accepted,
        DomainInvitationChange.Revoked => OrganizationInvitationChange.Revoked,
        DomainInvitationChange.Superseded => OrganizationInvitationChange.Superseded,
        _ => OrganizationInvitationChange.Unknown
    };

    public static OrganizationEnrollmentApprovalMode MapMode(DomainEnrollmentMode mode) => mode switch
    {
        DomainEnrollmentMode.Automatic => OrganizationEnrollmentApprovalMode.Automatic,
        DomainEnrollmentMode.RequiresApproval => OrganizationEnrollmentApprovalMode.RequiresApproval,
        _ => OrganizationEnrollmentApprovalMode.Unknown
    };

    public static OrganizationEnrollmentLinkStatus MapStatus(
        DomainEnrollmentLinkState status,
        DateTimeOffset expiresAtUtc,
        int reservedClaims,
        int maximumClaims,
        DateTimeOffset nowUtc) => status == DomainEnrollmentLinkState.Active && expiresAtUtc <= nowUtc
            ? OrganizationEnrollmentLinkStatus.Expired
            : status == DomainEnrollmentLinkState.Active && reservedClaims >= maximumClaims
                ? OrganizationEnrollmentLinkStatus.CapacityReached
                : MapStatus(status);

    public static OrganizationEnrollmentLinkStatus MapStatus(DomainEnrollmentLinkState status) => status switch
    {
        DomainEnrollmentLinkState.Active => OrganizationEnrollmentLinkStatus.Active,
        DomainEnrollmentLinkState.Disabled => OrganizationEnrollmentLinkStatus.Disabled,
        DomainEnrollmentLinkState.Rotated => OrganizationEnrollmentLinkStatus.Rotated,
        _ => OrganizationEnrollmentLinkStatus.Unknown
    };

    public static OrganizationEnrollmentClaimStatus MapStatus(DomainEnrollmentClaimState status) => status switch
    {
        DomainEnrollmentClaimState.Pending => OrganizationEnrollmentClaimStatus.Pending,
        DomainEnrollmentClaimState.Accepted => OrganizationEnrollmentClaimStatus.Accepted,
        DomainEnrollmentClaimState.Rejected => OrganizationEnrollmentClaimStatus.Rejected,
        _ => OrganizationEnrollmentClaimStatus.Unknown
    };

    public static OrganizationEnrollmentLinkChange MapChange(DomainEnrollmentLinkChange change) => change switch
    {
        DomainEnrollmentLinkChange.Created => OrganizationEnrollmentLinkChange.Created,
        DomainEnrollmentLinkChange.ClaimReserved => OrganizationEnrollmentLinkChange.ClaimReserved,
        DomainEnrollmentLinkChange.ClaimReleased => OrganizationEnrollmentLinkChange.ClaimReleased,
        DomainEnrollmentLinkChange.Disabled => OrganizationEnrollmentLinkChange.Disabled,
        DomainEnrollmentLinkChange.Rotated => OrganizationEnrollmentLinkChange.Rotated,
        _ => OrganizationEnrollmentLinkChange.Unknown
    };

    public static OrganizationEnrollmentClaimChange MapChange(DomainEnrollmentClaimChange change) => change switch
    {
        DomainEnrollmentClaimChange.Requested => OrganizationEnrollmentClaimChange.Requested,
        DomainEnrollmentClaimChange.Accepted => OrganizationEnrollmentClaimChange.Accepted,
        DomainEnrollmentClaimChange.Rejected => OrganizationEnrollmentClaimChange.Rejected,
        _ => OrganizationEnrollmentClaimChange.Unknown
    };
}
