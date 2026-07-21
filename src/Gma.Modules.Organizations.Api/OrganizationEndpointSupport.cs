namespace Gma.Modules.Organizations.Api;

using System.Security.Claims;
using Gma.Framework.Api.Results;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Modules.Organizations.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class OrganizationEndpointSupport
{
    public static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(OrganizationApplicationErrors.OrganizationNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.MembershipNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.MembershipRequired.Code, StatusCodes.Status403Forbidden),
        new(OrganizationApplicationErrors.OwnerRequired.Code, StatusCodes.Status403Forbidden),
        new(OrganizationApplicationErrors.SlugConflict.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.MembershipConflict.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.SelfServiceCreationDisabled.Code, StatusCodes.Status403Forbidden),
        new(OrganizationApplicationErrors.SubjectVerificationRequired.Code, StatusCodes.Status403Forbidden),
        new(OrganizationApplicationErrors.OwnershipTargetMustDiffer.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.InvitationNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.InvitationTokenInvalid.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.InvitationLifetimeInvalid.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.RecipientVerificationRequired.Code, StatusCodes.Status403Forbidden),
        new(OrganizationApplicationErrors.EnrollmentLinkNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.EnrollmentTokenInvalid.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.EnrollmentLifetimeInvalid.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.EnrollmentClaimLimitInvalid.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.EnrollmentClaimNotFound.Code, StatusCodes.Status404NotFound),
        new(OrganizationApplicationErrors.EnrollmentDecisionInvalid.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.LastActiveOwner.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.InvitationExpired.Code, StatusCodes.Status410Gone),
        new(OrganizationApplicationErrors.InvitationUnavailable.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.InvitationClaimedByAnotherSubject.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.EnrollmentConfigurationInvalid.Code, StatusCodes.Status400BadRequest),
        new(OrganizationApplicationErrors.EnrollmentLinkExpired.Code, StatusCodes.Status410Gone),
        new(OrganizationApplicationErrors.EnrollmentLinkUnavailable.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.EnrollmentLinkCapacityReached.Code, StatusCodes.Status409Conflict),
        new(OrganizationApplicationErrors.EnrollmentClaimUnavailable.Code, StatusCodes.Status409Conflict));

    public static bool TryGetSubject(HttpContext context, out string subjectId)
    {
        subjectId = (context.User.FindFirstValue(ApplicationClaimNames.Subject) ??
            context.User.FindFirstValue(ClaimTypes.NameIdentifier))?.Trim() ?? string.Empty;
        return subjectId.Length > 0;
    }

    public static string Actor(string subjectId) => $"user:{subjectId}";

    public static RouteHandlerBuilder RequireAssuranceWhenConfigured(
        RouteHandlerBuilder endpoint,
        AuthenticationAssuranceRequirement? requirement)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return requirement is null
            ? endpoint
            : endpoint.RequireAuthenticationAssurance(requirement);
    }

    public static void SetNoStoreHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}
