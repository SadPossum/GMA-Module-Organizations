namespace Gma.Modules.Organizations.Domain.Errors;

using Gma.Framework.Results;

public static class OrganizationDomainErrors
{
    public static readonly Error OrganizationIdRequired = new("Organizations.OrganizationIdRequired", "An organization id is required.");
    public static readonly Error MembershipIdRequired = new("Organizations.MembershipIdRequired", "A membership id is required.");
    public static readonly Error NameInvalid = new("Organizations.NameInvalid", "The organization name is invalid.");
    public static readonly Error SlugInvalid = new("Organizations.SlugInvalid", "The organization slug is invalid.");
    public static readonly Error CreationRequestFingerprintInvalid = new("Organizations.CreationRequestFingerprintInvalid", "The organization creation request fingerprint is invalid.");
    public static readonly Error SubjectInvalid = new("Organizations.SubjectInvalid", "The subject id is invalid.");
    public static readonly Error ActorInvalid = new("Organizations.ActorInvalid", "The actor id is invalid.");
    public static readonly Error EventIdRequired = new("Organizations.EventIdRequired", "A domain event id is required.");
    public static readonly Error VersionConflict = new("Organizations.VersionConflict", "The record version has changed.");
    public static readonly Error OrganizationNotActive = new("Organizations.OrganizationNotActive", "The organization is not active.");
    public static readonly Error OrganizationAlreadySuspended = new("Organizations.OrganizationAlreadySuspended", "The organization is already suspended.");
    public static readonly Error OrganizationNotSuspended = new("Organizations.OrganizationNotSuspended", "The organization is not suspended.");
    public static readonly Error OrganizationArchived = new("Organizations.OrganizationArchived", "The organization is archived.");
    public static readonly Error LastActiveOwner = new("Organizations.LastActiveOwner", "The last active owner cannot be removed, suspended, or demoted.");
    public static readonly Error MembershipNotActive = new("Organizations.MembershipNotActive", "The membership is not active.");
    public static readonly Error MembershipAlreadySuspended = new("Organizations.MembershipAlreadySuspended", "The membership is already suspended.");
    public static readonly Error MembershipNotSuspended = new("Organizations.MembershipNotSuspended", "The membership is not suspended.");
    public static readonly Error MembershipRemoved = new("Organizations.MembershipRemoved", "The membership was removed.");
    public static readonly Error MembershipAlreadyOwner = new("Organizations.MembershipAlreadyOwner", "The membership is already an owner.");
    public static readonly Error MembershipNotOwner = new("Organizations.MembershipNotOwner", "The membership is not an owner.");
    public static readonly Error InvitationIdRequired = new("Organizations.InvitationIdRequired", "An invitation id is required.");
    public static readonly Error InvitationRecipientInvalid = new("Organizations.InvitationRecipientInvalid", "The invitation recipient email is invalid.");
    public static readonly Error InvitationTokenDigestInvalid = new("Organizations.InvitationTokenDigestInvalid", "The invitation token digest is invalid.");
    public static readonly Error InvitationExpiryInvalid = new("Organizations.InvitationExpiryInvalid", "The invitation expiry is invalid.");
    public static readonly Error InvitationExpired = new("Organizations.InvitationExpired", "The invitation has expired.");
    public static readonly Error InvitationUnavailable = new("Organizations.InvitationUnavailable", "The invitation is no longer available.");
    public static readonly Error InvitationClaimedByAnotherSubject = new("Organizations.InvitationClaimedByAnotherSubject", "The invitation was already claimed by another subject.");
    public static readonly Error EnrollmentLinkIdRequired = new("Organizations.EnrollmentLinkIdRequired", "An enrollment link id is required.");
    public static readonly Error EnrollmentClaimIdRequired = new("Organizations.EnrollmentClaimIdRequired", "An enrollment claim id is required.");
    public static readonly Error EnrollmentConfigurationInvalid = new("Organizations.EnrollmentConfigurationInvalid", "The enrollment-link configuration is invalid.");
    public static readonly Error EnrollmentLinkExpired = new("Organizations.EnrollmentLinkExpired", "The enrollment link has expired.");
    public static readonly Error EnrollmentLinkUnavailable = new("Organizations.EnrollmentLinkUnavailable", "The enrollment link is unavailable.");
    public static readonly Error EnrollmentLinkCapacityReached = new("Organizations.EnrollmentLinkCapacityReached", "The enrollment link has reached its claim limit.");
    public static readonly Error EnrollmentClaimExpiryInvalid = new("Organizations.EnrollmentClaimExpiryInvalid", "The enrollment claim expiry is invalid.");
    public static readonly Error EnrollmentClaimExpired = new("Organizations.EnrollmentClaimExpired", "The enrollment claim has expired.");
    public static readonly Error EnrollmentClaimUnavailable = new("Organizations.EnrollmentClaimUnavailable", "The enrollment claim is unavailable.");
    public static readonly Error ScopeStateInvalid = new("Organizations.ScopeStateInvalid", "The organization scope state is invalid.");
    public static readonly Error ScopeDestroyOperationInvalid = new("Organizations.ScopeDestroyOperationInvalid", "The organization scope destruction operation is invalid.");
    public static readonly Error ScopeDestroyReceiptInvalid = new("Organizations.ScopeDestroyReceiptInvalid", "The organization scope destruction receipt is invalid.");
}
