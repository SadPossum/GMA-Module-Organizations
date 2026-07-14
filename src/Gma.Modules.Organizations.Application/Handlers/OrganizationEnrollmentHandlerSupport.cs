namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.Extensions.Options;
using DomainApprovalMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;

internal static class OrganizationEnrollmentHandlerSupport
{
    public static Result<int> ResolveLifetimeHours(int? requested, IOptions<OrganizationsOptions> options)
    {
        int lifetime = requested ?? options.Value.EnrollmentDefaultLifetimeHours;
        return lifetime is >= 1 && lifetime <= options.Value.EnrollmentMaxLifetimeHours
            ? Result.Success(lifetime)
            : Result.Failure<int>(OrganizationApplicationErrors.EnrollmentLifetimeInvalid);
    }

    public static Result<int> ValidateMaximumClaims(int maximumClaims, IOptions<OrganizationsOptions> options) =>
        maximumClaims is >= 1 && maximumClaims <= options.Value.EnrollmentMaxClaims
            ? Result.Success(maximumClaims)
            : Result.Failure<int>(OrganizationApplicationErrors.EnrollmentClaimLimitInvalid);

    public static Result<DomainApprovalMode> MapMode(
        Gma.Modules.Organizations.Contracts.OrganizationEnrollmentApprovalMode mode) => mode switch
    {
        Gma.Modules.Organizations.Contracts.OrganizationEnrollmentApprovalMode.Automatic =>
            Result.Success(DomainApprovalMode.Automatic),
        Gma.Modules.Organizations.Contracts.OrganizationEnrollmentApprovalMode.RequiresApproval =>
            Result.Success(DomainApprovalMode.RequiresApproval),
        _ => Result.Failure<DomainApprovalMode>(OrganizationApplicationErrors.EnrollmentClaimLimitInvalid)
    };
}
