namespace Gma.Modules.Organizations.Persistence.Repositories;

using Gma.Framework.Results;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

internal sealed class OrganizationEnrollmentClaimInspector(OrganizationsDbContext dbContext)
    : IOrganizationEnrollmentClaimInspector
{
    public async Task<OrganizationEnrollmentClaimDto?> FindAsync(
        Guid organizationId,
        Guid enrollmentLinkId,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(enrollmentLinkId, Guid.Empty);
        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        if (subject.IsFailure)
        {
            throw new ArgumentException(subject.Error.Message, nameof(subjectId));
        }

        ClaimSnapshot? claim = await dbContext.EnrollmentClaims
            .AsNoTracking()
            .Where(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.EnrollmentLinkId == enrollmentLinkId &&
                candidate.SubjectId == subject.Value.Value)
            .Select(candidate => new ClaimSnapshot(
                candidate.Id,
                candidate.EnrollmentLinkId,
                candidate.OrganizationId,
                candidate.SubjectId,
                candidate.Status,
                candidate.MembershipId,
                candidate.Version,
                candidate.CreatedAtUtc,
                candidate.LastChangedAtUtc,
                candidate.DecisionExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return claim is null
            ? null
            : new OrganizationEnrollmentClaimDto(
                claim.ClaimId,
                claim.EnrollmentLinkId,
                claim.OrganizationId,
                claim.SubjectId,
                OrganizationMappings.MapStatus(claim.Status),
                claim.MembershipId,
                claim.Version,
                claim.CreatedAtUtc,
                claim.LastChangedAtUtc)
            {
                DecisionExpiresAtUtc = claim.DecisionExpiresAtUtc
            };
    }

    private sealed record ClaimSnapshot(
        Guid ClaimId,
        Guid EnrollmentLinkId,
        Guid OrganizationId,
        string SubjectId,
        OrganizationEnrollmentClaimState Status,
        Guid? MembershipId,
        long Version,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset LastChangedAtUtc,
        DateTimeOffset? DecisionExpiresAtUtc);
}
