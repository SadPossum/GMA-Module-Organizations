namespace Gma.Modules.Organizations.Persistence;

using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;

internal sealed partial class OrganizationScopeLifecycleService
{
    private static OrganizationScopeExportRecord Map(
        Organization organization) =>
        new OrganizationScopeOrganizationExportRecord(
            organization.Id,
            organization.Name,
            organization.Slug,
            OrganizationMappings.MapStatus(organization.Status),
            organization.ActiveOwnerCount,
            organization.Version,
            organization.CreatedBy,
            organization.CreatedAtUtc,
            organization.LastChangedBy,
            organization.LastChangedAtUtc);

    private static OrganizationScopeExportRecord Map(
        OrganizationMembership membership) =>
        new OrganizationScopeMembershipExportRecord(
            membership.Id,
            membership.OrganizationId,
            membership.SubjectId,
            OrganizationMappings.MapRole(membership.Role),
            OrganizationMappings.MapStatus(membership.Status),
            membership.Version,
            membership.CreatedBy,
            membership.JoinedAtUtc,
            membership.LastChangedBy,
            membership.LastChangedAtUtc);

    private static OrganizationScopeExportRecord Map(
        OrganizationInvitation invitation) =>
        new OrganizationScopeInvitationExportRecord(
            invitation.Id,
            invitation.OrganizationId,
            invitation.InviterSubjectId,
            invitation.RecipientEmail,
            invitation.TokenVersion,
            invitation.ExpiresAtUtc,
            OrganizationMappings.MapStatus(invitation.Status),
            invitation.AcceptedSubjectId,
            invitation.AcceptedMembershipId,
            invitation.AcceptedAtUtc,
            invitation.Version,
            invitation.CreatedBy,
            invitation.CreatedAtUtc,
            invitation.LastChangedBy,
            invitation.LastChangedAtUtc,
            invitation.ReplacesInvitationId,
            invitation.ReplacesInvitationVersion);

    private static OrganizationScopeExportRecord Map(
        OrganizationEnrollmentLink link) =>
        new OrganizationScopeEnrollmentLinkExportRecord(
            link.Id,
            link.OrganizationId,
            link.CreatorSubjectId,
            link.TokenVersion,
            link.ExpiresAtUtc,
            link.MaximumClaims,
            link.ReservedClaims,
            OrganizationMappings.MapMode(link.ApprovalMode),
            OrganizationMappings.MapStatus(link.Status),
            link.Version,
            link.CreatedBy,
            link.CreatedAtUtc,
            link.LastChangedBy,
            link.LastChangedAtUtc,
            link.ReplacesEnrollmentLinkId,
            link.ReplacesEnrollmentLinkVersion);

    private static OrganizationScopeExportRecord Map(
        OrganizationEnrollmentClaim claim) =>
        new OrganizationScopeEnrollmentClaimExportRecord(
            claim.Id,
            claim.OrganizationId,
            claim.EnrollmentLinkId,
            claim.SubjectId,
            OrganizationMappings.MapStatus(claim.Status),
            claim.MembershipId,
            claim.DecisionExpiresAtUtc,
            claim.Version,
            claim.CreatedAtUtc,
            claim.LastChangedBy,
            claim.LastChangedAtUtc);
}
