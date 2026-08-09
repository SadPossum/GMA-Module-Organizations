namespace Gma.Modules.Organizations.Application;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Errors;

public static class OrganizationApplicationErrors
{
    public static readonly Error OrganizationNotFound = new("Organizations.OrganizationNotFound", "The organization was not found.");
    public static readonly Error MembershipNotFound = new("Organizations.MembershipNotFound", "The membership was not found.");
    public static readonly Error MembershipRequired = new("Organizations.MembershipRequired", "An active organization membership is required.");
    public static readonly Error OwnerRequired = new("Organizations.OwnerRequired", "An active organization owner membership is required.");
    public static readonly Error SlugConflict = new("Organizations.SlugConflict", "The organization slug is already in use.");
    public static readonly Error CreationOperationRequired = new("Organizations.CreationOperationRequired", "An organization creation operation id is required.");
    public static readonly Error CreationOperationConflict = new("Organizations.CreationOperationConflict", "The organization creation operation id is already bound to different inputs or an unavailable result.");
    public static readonly Error MembershipConflict = new("Organizations.MembershipConflict", "The subject already has an organization membership.");
    public static readonly Error MembershipChangeRejected = new("Organizations.MembershipChangeRejected", "The membership change is not available through this operation.");
    public static readonly Error SelfServiceCreationDisabled = new("Organizations.SelfServiceCreationDisabled", "Self-service organization creation is disabled.");
    public static readonly Error SubjectVerificationRequired = new("Organizations.SubjectVerificationRequired", "A verified subject identity is required.");
    public static readonly Error OwnershipTargetMustDiffer = new("Organizations.OwnershipTargetMustDiffer", "Ownership must be transferred to another active member.");
    public static readonly Error OrganizationLifecycleActionInvalid = new("Organizations.OrganizationLifecycleActionInvalid", "The organization lifecycle action is invalid.");
    public static readonly Error InvitationNotFound = new("Organizations.InvitationNotFound", "The invitation was not found.");
    public static readonly Error InvitationTokenInvalid = new("Organizations.InvitationTokenInvalid", "The invitation token is invalid.");
    public static readonly Error InvitationLifetimeInvalid = new("Organizations.InvitationLifetimeInvalid", "The invitation lifetime is invalid.");
    public static readonly Error RecipientVerificationRequired = new("Organizations.RecipientVerificationRequired", "Verified recipient ownership is required to accept this invitation.");
    public static readonly Error EnrollmentLinkNotFound = new("Organizations.EnrollmentLinkNotFound", "The enrollment link was not found.");
    public static readonly Error EnrollmentTokenInvalid = new("Organizations.EnrollmentTokenInvalid", "The enrollment token is invalid.");
    public static readonly Error EnrollmentLifetimeInvalid = new("Organizations.EnrollmentLifetimeInvalid", "The enrollment-link lifetime is invalid.");
    public static readonly Error EnrollmentClaimLimitInvalid = new("Organizations.EnrollmentClaimLimitInvalid", "The enrollment-link claim limit is invalid.");
    public static readonly Error EnrollmentClaimNotFound = new("Organizations.EnrollmentClaimNotFound", "The enrollment claim was not found.");
    public static readonly Error EnrollmentDecisionInvalid = new("Organizations.EnrollmentDecisionInvalid", "The enrollment decision is invalid.");
    public static readonly Error JoinRequestConflict = new("Organizations.JoinRequestConflict", "The subject already has a pending organization join request.");
    public static readonly Error JoinAdmissionRejected = new("Organizations.JoinAdmissionRejected", "The product is not ready to admit this subject.");
    public static readonly Error MutationRejected = new("Organizations.MutationRejected", "The organization mutation is not currently permitted.");
    public static readonly Error MutationAdmissionUnavailable = new("Organizations.MutationAdmissionUnavailable", "Organization mutation admission is temporarily unavailable.");
    public static readonly Error JoinSourceIdRequired = new("Organizations.JoinSourceIdRequired", "A join-source id is required.");
    public static readonly Error JoinSourceIssuanceConflict = new("Organizations.JoinSourceIssuanceConflict", "The join-source id is already bound to different issuance inputs.");
    public static readonly Error JoinSourceManagementRequired = new("Organizations.JoinSourceManagementRequired", "An active organization owner or authorized join-source manager is required.");
    public static readonly Error JoinSourceAuthorizationUnavailable = new("Organizations.JoinSourceAuthorizationUnavailable", "Join-source authorization is temporarily unavailable.");
    public static Error VersionConflict => OrganizationDomainErrors.VersionConflict;
    public static Error LastActiveOwner => OrganizationDomainErrors.LastActiveOwner;
    public static Error OrganizationNotActive => OrganizationDomainErrors.OrganizationNotActive;
    public static Error OrganizationAlreadySuspended => OrganizationDomainErrors.OrganizationAlreadySuspended;
    public static Error OrganizationNotSuspended => OrganizationDomainErrors.OrganizationNotSuspended;
    public static Error OrganizationArchived => OrganizationDomainErrors.OrganizationArchived;
    public static Error InvitationExpired => OrganizationDomainErrors.InvitationExpired;
    public static Error InvitationUnavailable => OrganizationDomainErrors.InvitationUnavailable;
    public static Error InvitationClaimedByAnotherSubject => OrganizationDomainErrors.InvitationClaimedByAnotherSubject;
    public static Error EnrollmentConfigurationInvalid => OrganizationDomainErrors.EnrollmentConfigurationInvalid;
    public static Error EnrollmentLinkExpired => OrganizationDomainErrors.EnrollmentLinkExpired;
    public static Error EnrollmentLinkUnavailable => OrganizationDomainErrors.EnrollmentLinkUnavailable;
    public static Error EnrollmentLinkCapacityReached => OrganizationDomainErrors.EnrollmentLinkCapacityReached;
    public static Error EnrollmentClaimExpired => OrganizationDomainErrors.EnrollmentClaimExpired;
    public static Error EnrollmentClaimUnavailable => OrganizationDomainErrors.EnrollmentClaimUnavailable;
}
