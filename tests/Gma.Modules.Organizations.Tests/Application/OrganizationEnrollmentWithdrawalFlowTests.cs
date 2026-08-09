namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ContractEnrollmentApprovalMode =
    Gma.Modules.Organizations.Contracts.OrganizationEnrollmentApprovalMode;

public sealed partial class OrganizationEnrollmentFlowTests
{
    [Fact]
    public async Task Applicant_can_withdraw_a_pending_claim_and_replay_the_result()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentOutcomeDto pending = await CreatePendingClaimAsync(
            services,
            repository);
        var handler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationEnrollmentClaim claim = Assert.Single(repository.EnrollmentClaims);

        Result<OrganizationEnrollmentOutcomeDto> withdrawn = await handler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Claim.ClaimId,
                pending.Claim.Version,
                "member",
                "user:member"),
            CancellationToken.None);
        int eventCount = claim.DomainEvents.Count;
        Result<OrganizationEnrollmentOutcomeDto> replay = await handler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Claim.ClaimId,
                pending.Claim.Version,
                "member",
                "user:member"),
            CancellationToken.None);

        Assert.True(withdrawn.IsSuccess, withdrawn.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Withdrawn, withdrawn.Value.Claim.Status);
        Assert.Null(withdrawn.Value.Membership);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Withdrawn, replay.Value.Claim.Status);
        Assert.Equal(eventCount, claim.DomainEvents.Count);
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
        Assert.DoesNotContain(repository.Memberships, item => item.SubjectId == "member");
    }

    [Fact]
    public async Task Another_subject_cannot_observe_or_withdraw_the_claim()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentOutcomeDto pending = await CreatePendingClaimAsync(
            services,
            repository);
        var handler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);

        Result<OrganizationEnrollmentOutcomeDto> result = await handler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Claim.ClaimId,
                pending.Claim.Version,
                "Member",
                "user:Member"),
            CancellationToken.None);

        Assert.Equal(OrganizationApplicationErrors.EnrollmentClaimNotFound, result.Error);
        Assert.Equal(
            OrganizationEnrollmentClaimState.Pending,
            Assert.Single(repository.EnrollmentClaims).Status);
        Assert.Equal(1, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Withdrawal_terminalizes_an_overdue_claim_as_expired_and_is_retry_safe()
    {
        TestOrganizationRepository repository = CreateRepository();
        TestClock clock = new(Now);
        using ServiceProvider services = CreateServices(repository, clock);
        OrganizationEnrollmentOutcomeDto pending = await CreatePendingClaimAsync(
            services,
            repository);
        var handler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        OrganizationEnrollmentClaim claim = Assert.Single(repository.EnrollmentClaims);
        clock.UtcNow = Now.AddDays(8);

        WithdrawOrganizationJoinRequestCommand command = new(
            organization.Id,
            pending.Claim.ClaimId,
            pending.Claim.Version,
            "member",
            "user:member");
        Result<OrganizationEnrollmentOutcomeDto> expired = await handler.HandleAsync(
            command,
            CancellationToken.None);
        int eventCount = claim.DomainEvents.Count;
        Result<OrganizationEnrollmentOutcomeDto> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(expired.IsSuccess, expired.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Expired, expired.Value.Claim.Status);
        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Expired, replay.Value.Claim.Status);
        Assert.Equal(eventCount, claim.DomainEvents.Count);
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    [Fact]
    public async Task Withdrawal_does_not_rewrite_a_terminal_source_counter()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentOutcomeDto pending = await CreatePendingClaimAsync(
            services,
            repository);
        OrganizationEnrollmentLink link = Assert.Single(repository.EnrollmentLinks);
        Assert.True(link.Disable(
            link.Version,
            "user:owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        var handler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);

        Result<OrganizationEnrollmentOutcomeDto> withdrawn = await handler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Claim.ClaimId,
                pending.Claim.Version,
                "member",
                "user:member"),
            CancellationToken.None);

        Assert.True(withdrawn.IsSuccess, withdrawn.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Withdrawn, withdrawn.Value.Claim.Status);
        Assert.Equal(1, link.ReservedClaims);
    }

    [Fact]
    public async Task Product_admission_cannot_veto_applicant_withdrawal()
    {
        TestOrganizationRepository repository = CreateRepository();
        RecordingJoinPolicy policy = new();
        using ServiceProvider services = CreateServices(
            repository,
            new TestClock(Now),
            policy);
        OrganizationEnrollmentOutcomeDto pending = await CreatePendingClaimAsync(
            services,
            repository);
        policy.Contexts.Clear();
        policy.Decision = OrganizationJoinAdmissionDecision.Denied;
        var handler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);

        Result<OrganizationEnrollmentOutcomeDto> withdrawn = await handler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Claim.ClaimId,
                pending.Claim.Version,
                "member",
                "user:member"),
            CancellationToken.None);

        Assert.True(withdrawn.IsSuccess, withdrawn.Error.Code);
        Assert.Empty(policy.Contexts);
    }

    [Fact]
    public async Task A_withdrawn_claim_cannot_be_reused_through_the_same_source()
    {
        TestOrganizationRepository repository = CreateRepository();
        using ServiceProvider services = CreateServices(repository, new TestClock(Now));
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services,
            repository,
            ContractEnrollmentApprovalMode.RequiresApproval,
            maximumClaims: 1);
        var claimHandler = services.GetRequiredService<ICommandHandler<
            ClaimOrganizationEnrollmentLinkCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Result<OrganizationEnrollmentOutcomeDto> pending = await claimHandler.HandleAsync(
            new ClaimOrganizationEnrollmentLinkCommand(
                issued.Token,
                "member",
                "user:member"),
            CancellationToken.None);
        var withdrawHandler = services.GetRequiredService<ICommandHandler<
            WithdrawOrganizationJoinRequestCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Organization organization = Assert.Single(repository.Organizations);
        Assert.True((await withdrawHandler.HandleAsync(
            new WithdrawOrganizationJoinRequestCommand(
                organization.Id,
                pending.Value.Claim.ClaimId,
                pending.Value.Claim.Version,
                "member",
                "user:member"),
            CancellationToken.None)).IsSuccess);

        Result<OrganizationEnrollmentOutcomeDto> replayedSource =
            await claimHandler.HandleAsync(
                new ClaimOrganizationEnrollmentLinkCommand(
                    issued.Token,
                    "member",
                    "user:member"),
                CancellationToken.None);

        Assert.Equal(OrganizationDomainErrors.EnrollmentClaimUnavailable, replayedSource.Error);
        Assert.Single(repository.EnrollmentClaims);
        Assert.Equal(0, Assert.Single(repository.EnrollmentLinks).ReservedClaims);
    }

    private static async Task<OrganizationEnrollmentOutcomeDto> CreatePendingClaimAsync(
        ServiceProvider services,
        TestOrganizationRepository repository)
    {
        OrganizationEnrollmentLinkIssuedDto issued = await IssueAsync(
            services,
            repository,
            ContractEnrollmentApprovalMode.RequiresApproval,
            maximumClaims: 1);
        var handler = services.GetRequiredService<ICommandHandler<
            ClaimOrganizationEnrollmentLinkCommand,
            OrganizationEnrollmentOutcomeDto>>();
        Result<OrganizationEnrollmentOutcomeDto> pending = await handler.HandleAsync(
            new ClaimOrganizationEnrollmentLinkCommand(
                issued.Token,
                "member",
                "user:member"),
            CancellationToken.None);
        Assert.True(pending.IsSuccess, pending.Error.Code);
        Assert.Equal(OrganizationEnrollmentClaimStatus.Pending, pending.Value.Claim.Status);
        return pending.Value;
    }
}
