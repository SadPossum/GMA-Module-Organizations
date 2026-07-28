namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;

internal static class OrganizationEnrollmentClaimExpiry
{
    public static Result<OrganizationEnrollmentOutcomeDto> Expire(
        OrganizationEnrollmentClaim claim,
        OrganizationEnrollmentLink link,
        long expectedVersion,
        DateTimeOffset nowUtc,
        IIdGenerator ids)
    {
        Result expired = claim.Expire(
            expectedVersion,
            OrganizationLifecycleMaintenance.ActorId,
            ids.NewId(),
            nowUtc);
        if (expired.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentOutcomeDto>(expired.Error);
        }

        if (link.Status == OrganizationEnrollmentLinkState.Active && link.ReservedClaims > 0)
        {
            Result released = link.ReleaseClaim(
                link.Version,
                OrganizationLifecycleMaintenance.ActorId,
                ids.NewId(),
                nowUtc);
            if (released.IsFailure)
            {
                return Result.Failure<OrganizationEnrollmentOutcomeDto>(released.Error);
            }
        }

        return Result.Success(new OrganizationEnrollmentOutcomeDto(claim.ToDto(), null));
    }
}
