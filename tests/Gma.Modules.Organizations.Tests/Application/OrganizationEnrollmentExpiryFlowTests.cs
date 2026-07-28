namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed partial class OrganizationEnrollmentFlowTests
{
    [Fact]
    public async Task Pending_claim_remains_approvable_after_its_source_link_expires()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 2);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        clock.UtcNow = Now.AddHours(25);

        Organization organization = Assert.Single(repository.Organizations);
        var approved = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Approve,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);

        Assert.True(approved.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Accepted, approved.Value.Claim.Status);
        Assert.Single(repository.Memberships, item => item.SubjectId == "member");
    }

    [Fact]
    public async Task Owner_decision_terminalizes_an_overdue_claim_without_creating_membership()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        clock.UtcNow = Now.AddDays(8);

        Organization organization = Assert.Single(repository.Organizations);
        var result = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Approve,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Expired, result.Value.Claim.Status);
        Assert.Null(result.Value.Membership);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId == "member");
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Applicant_retry_terminalizes_an_overdue_pending_claim()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        clock.UtcNow = Now.AddDays(8);

        var retry = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);

        Assert.True(pending.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Expired, retry.Value.Claim.Status);
        Assert.Null(retry.Value.Membership);
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);

        var secondRetry = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        Assert.True(secondRetry.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Expired, secondRetry.Value.Claim.Status);
    }

    [Fact]
    public async Task Pending_claim_can_be_rejected_after_its_link_is_durably_expired()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services, repository, OrganizationEnrollmentApprovalMode.RequiresApproval, maximumClaims: 1);
        var claimHandler = services.GetRequiredService<
            ICommandHandler<ClaimOrganizationEnrollmentLinkCommand, OrganizationEnrollmentOutcomeDto>>();
        var resolveHandler = services.GetRequiredService<
            ICommandHandler<ResolveOrganizationJoinRequestCommand, OrganizationEnrollmentOutcomeDto>>();
        var pending = await claimHandler.HandleAsync(new ClaimOrganizationEnrollmentLinkCommand(
            issued.Token, "member", "user:member"), CancellationToken.None);
        OrganizationEnrollmentLink link = Assert.Single(repository.EnrollmentLinks);
        clock.UtcNow = Now.AddHours(25);
        Assert.True(link.Expire(
            link.Version, "system:lifecycle", Guid.NewGuid(), clock.UtcNow).IsSuccess);

        Organization organization = Assert.Single(repository.Organizations);
        var rejected = await resolveHandler.HandleAsync(new ResolveOrganizationJoinRequestCommand(
            organization.Id, pending.Value.Claim.ClaimId, OrganizationJoinRequestDecision.Reject,
            pending.Value.Claim.Version, "owner", "user:owner"), CancellationToken.None);

        Assert.True(rejected.IsSuccess);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Rejected, rejected.Value.Claim.Status);
        Assert.Equal(1, link.ReservedClaims);
    }
}
