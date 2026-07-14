namespace Gma.Modules.Organizations.Api.Requests;

using Gma.Modules.Organizations.Contracts;

public sealed record CreateOrganizationRequest(string Name, string Slug);

public sealed record UpdateOrganizationRequest(string Name, string Slug, long ExpectedVersion);

public sealed record OrganizationLifecycleRequest(long ExpectedVersion);

public sealed record OrganizationMembershipLifecycleRequest(
    string TargetSubjectId,
    long ExpectedOrganizationVersion,
    long ExpectedMembershipVersion);

public sealed record TransferOrganizationOwnershipRequest(
    string TargetSubjectId,
    long ExpectedOrganizationVersion,
    long ExpectedCurrentOwnerVersion,
    long ExpectedTargetVersion);

public sealed record CreateOrganizationInvitationRequest(string? RecipientEmail, int? LifetimeHours);

public sealed record RevokeOrganizationInvitationRequest(long ExpectedVersion);

public sealed record ReissueOrganizationInvitationRequest(long ExpectedVersion, int? LifetimeHours);

public sealed record AcceptOrganizationInvitationRequest(string Token);

public sealed record PreviewOrganizationInvitationRequest(string Token);

public sealed record CreateOrganizationEnrollmentLinkRequest(
    int? LifetimeHours,
    int MaximumClaims,
    OrganizationEnrollmentApprovalMode ApprovalMode);

public sealed record ChangeOrganizationEnrollmentLinkRequest(
    long ExpectedVersion,
    int? ReplacementLifetimeHours);

public sealed record ResolveOrganizationJoinRequestRequest(long ExpectedVersion);

public sealed record ClaimOrganizationEnrollmentLinkRequest(string Token);

public sealed record PreviewOrganizationEnrollmentLinkRequest(string Token);
