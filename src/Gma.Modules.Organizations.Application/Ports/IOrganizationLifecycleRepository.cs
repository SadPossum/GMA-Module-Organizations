namespace Gma.Modules.Organizations.Application.Ports;

using Gma.Modules.Organizations.Domain.Aggregates;

public sealed record OrganizationEnrollmentClaimExpiryCandidate(
    OrganizationEnrollmentClaim Claim,
    OrganizationEnrollmentLink Link);

public interface IOrganizationLifecycleRepository
{
    Task<OrganizationInvitation[]> ListDueInvitationsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<OrganizationEnrollmentClaimExpiryCandidate[]> ListDueEnrollmentClaimsAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<OrganizationEnrollmentLink[]> ListDueEnrollmentLinksAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);
}
