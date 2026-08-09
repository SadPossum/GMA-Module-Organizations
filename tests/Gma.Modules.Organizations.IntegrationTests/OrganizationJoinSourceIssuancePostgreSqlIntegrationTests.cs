namespace Gma.Modules.Organizations.IntegrationTests;

using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationJoinSourceIssuancePostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 13, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Issuance_is_serialized_per_source_identity()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organization_join_source_issuance_tests")
                .Build();
        await postgreSql.StartAsync();
        await using ServiceProvider provider = CreateProvider(postgreSql.GetConnectionString());
        await MigrateAsync(provider);

        Guid organizationId = Guid.NewGuid();
        Result<OrganizationMembershipSummaryDto> created = await DispatchAsync(
            provider,
            new CreateOrganizationCommand(
                organizationId,
                "Harbor House",
                "harbor-house",
                "owner",
                "user:owner"));
        Assert.True(created.IsSuccess, created.Error.Code);

        Guid exactSourceId = Guid.NewGuid();
        IssueOrganizationInvitationCommand exactCommand = Invitation(
            organizationId,
            exactSourceId,
            recipientEmail: "member@example.com");
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>[] exactResults =
            await Task.WhenAll(
                DispatchAsync(provider, exactCommand),
                DispatchAsync(provider, exactCommand));

        Assert.All(exactResults, result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Single(exactResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.Issued &&
            result.Value.Token is not null);
        Assert.Single(exactResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.AlreadyIssued &&
            result.Value.Token is null);

        Guid changedSourceId = Guid.NewGuid();
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>[] changedResults =
            await Task.WhenAll(
                DispatchAsync(provider, Enrollment(organizationId, changedSourceId, 10)),
                DispatchAsync(provider, Enrollment(organizationId, changedSourceId, 11)));

        Assert.Single(changedResults, result => result.IsSuccess);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> changedFailure =
            Assert.Single(changedResults, result => result.IsFailure);
        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceIssuanceConflict.Code,
            changedFailure.Error.Code);

        Guid crossKindSourceId = Guid.NewGuid();
        Task<Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>> invitationTask =
            DispatchAsync(provider, Invitation(organizationId, crossKindSourceId, null));
        Task<Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>> enrollmentTask =
            DispatchAsync(provider, Enrollment(organizationId, crossKindSourceId, 10));
        await Task.WhenAll(invitationTask, enrollmentTask);

        Assert.NotEqual(invitationTask.Result.IsSuccess, enrollmentTask.Result.IsSuccess);
        Error crossKindError = invitationTask.Result.IsFailure
            ? invitationTask.Result.Error
            : enrollmentTask.Result.Error;
        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceIssuanceConflict.Code,
            crossKindError.Code);

        OrganizationInvitationDto invitationPredecessor = exactResults.Single(result =>
            result.IsSuccess &&
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.Issued).Value.Source!;
        Guid invitationReplacementId = Guid.NewGuid();
        ReissueOrganizationInvitationCommand reissue = Reissue(
            organizationId,
            invitationPredecessor,
            invitationReplacementId);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>[] reissueResults =
            await Task.WhenAll(
                DispatchAsync(provider, reissue),
                DispatchAsync(provider, reissue));

        Assert.All(reissueResults, result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Single(reissueResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.Issued &&
            result.Value.Token is not null);
        Assert.Single(reissueResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.AlreadyIssued &&
            result.Value.Token is null);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> changedReissue =
            await DispatchAsync(provider, reissue with { LifetimeHours = 48 });
        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceIssuanceConflict.Code,
            changedReissue.Error.Code);

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> issuedForRotation =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 20));
        Assert.True(issuedForRotation.IsSuccess, issuedForRotation.Error.Code);
        Guid enrollmentReplacementId = Guid.NewGuid();
        RotateOrganizationEnrollmentLinkCommand rotate = Rotate(
            organizationId,
            issuedForRotation.Value.Source!,
            enrollmentReplacementId);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>[] rotationResults =
            await Task.WhenAll(
                DispatchAsync(provider, rotate),
                DispatchAsync(provider, rotate));

        Assert.All(rotationResults, result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Single(rotationResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.Issued &&
            result.Value.Token is not null);
        Assert.Single(rotationResults, result =>
            result.Value.Outcome == OrganizationJoinSourceIssuanceOutcome.AlreadyIssued &&
            result.Value.Token is null);
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> changedRotation =
            await DispatchAsync(
                provider,
                rotate with { ReplacementLifetimeHours = 48 });
        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceIssuanceConflict.Code,
            changedRotation.Error.Code);

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> competingPredecessor =
            await DispatchAsync(
                provider,
                Invitation(organizationId, Guid.NewGuid(), "competing@example.com"));
        Assert.True(competingPredecessor.IsSuccess, competingPredecessor.Error.Code);
        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>[] competingResults =
            await Task.WhenAll(
                DispatchAsync(provider, Reissue(
                    organizationId,
                    competingPredecessor.Value.Source!,
                    Guid.NewGuid())),
                DispatchAsync(provider, Reissue(
                    organizationId,
                    competingPredecessor.Value.Source!,
                    Guid.NewGuid())));
        Assert.Single(competingResults, result => result.IsSuccess);
        Assert.Single(competingResults, result =>
            result.IsFailure &&
            result.Error.Code == OrganizationDomainErrors.VersionConflict.Code);

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> collisionInvitation =
            await DispatchAsync(
                provider,
                Invitation(organizationId, Guid.NewGuid(), "collision@example.com"));
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> collisionEnrollment =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 30));
        Assert.True(collisionInvitation.IsSuccess, collisionInvitation.Error.Code);
        Assert.True(collisionEnrollment.IsSuccess, collisionEnrollment.Error.Code);
        Guid replacementCollisionId = Guid.NewGuid();
        Task<Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>> reissueCollisionTask =
            DispatchAsync(provider, Reissue(
                organizationId,
                collisionInvitation.Value.Source!,
                replacementCollisionId));
        Task<Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>> rotateCollisionTask =
            DispatchAsync(provider, Rotate(
                organizationId,
                collisionEnrollment.Value.Source!,
                replacementCollisionId));
        await Task.WhenAll(reissueCollisionTask, rotateCollisionTask);
        Assert.NotEqual(reissueCollisionTask.Result.IsSuccess, rotateCollisionTask.Result.IsSuccess);
        Error replacementCollisionError = reissueCollisionTask.Result.IsFailure
            ? reissueCollisionTask.Result.Error
            : rotateCollisionTask.Result.Error;
        Assert.Equal(
            OrganizationApplicationErrors.JoinSourceIssuanceConflict.Code,
            replacementCollisionError.Code);

        await using AsyncServiceScope verificationScope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = verificationScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        Assert.Single(await dbContext.Invitations
            .Where(item => item.Id == exactSourceId)
            .ToArrayAsync());
        Assert.Single(await dbContext.EnrollmentLinks
            .Where(item => item.Id == changedSourceId)
            .ToArrayAsync());
        int crossKindRows = await dbContext.Invitations.CountAsync(
                item => item.Id == crossKindSourceId) +
            await dbContext.EnrollmentLinks.CountAsync(
                item => item.Id == crossKindSourceId);
        Assert.Equal(1, crossKindRows);
        Assert.Single(await dbContext.Invitations
            .Where(item => item.ReplacesInvitationId == invitationPredecessor.InvitationId)
            .ToArrayAsync());
        Assert.Single(await dbContext.EnrollmentLinks
            .Where(item => item.ReplacesEnrollmentLinkId ==
                           issuedForRotation.Value.Source!.EnrollmentLinkId)
            .ToArrayAsync());
        Assert.Single(await dbContext.Invitations
            .Where(item => item.ReplacesInvitationId ==
                           competingPredecessor.Value.Source!.InvitationId)
            .ToArrayAsync());
        int replacementCollisionRows = await dbContext.Invitations.CountAsync(
                item => item.Id == replacementCollisionId) +
            await dbContext.EnrollmentLinks.CountAsync(
                item => item.Id == replacementCollisionId);
        Assert.Equal(1, replacementCollisionRows);

        await VerifyGovernanceCoordinationAsync(
            provider,
            organizationId,
            created.Value.Organization.Version);
        await VerifyJoinSubjectCoordinationAsync(provider, organizationId);
    }

    private static async Task VerifyJoinSubjectCoordinationAsync(
        ServiceProvider provider,
        Guid organizationId)
    {
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> firstLink =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 5));
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> secondLink =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 5));
        Assert.True(firstLink.IsSuccess, firstLink.Error.Code);
        Assert.True(secondLink.IsSuccess, secondLink.Error.Code);

        Result<OrganizationEnrollmentOutcomeDto>[] competing = await Task.WhenAll(
            DispatchAsync(
                provider,
                new ClaimOrganizationEnrollmentLinkCommand(
                    Assert.IsType<string>(firstLink.Value.Token),
                    "join-race-member",
                    "user:join-race-member")),
            DispatchAsync(
                provider,
                new ClaimOrganizationEnrollmentLinkCommand(
                    Assert.IsType<string>(secondLink.Value.Token),
                    "join-race-member",
                    "user:join-race-member")));

        Assert.Single(competing, result => result.IsSuccess);
        Assert.Single(competing, result =>
            result.IsFailure &&
            result.Error.Code == OrganizationApplicationErrors.JoinRequestConflict.Code);

        await using (AsyncServiceScope verificationScope = provider.CreateAsyncScope())
        {
            OrganizationsDbContext dbContext = verificationScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            Assert.Single(await dbContext.EnrollmentClaims
                .Where(item =>
                    item.OrganizationId == organizationId &&
                    item.SubjectId == "join-race-member")
                .ToArrayAsync());
            int reservedClaims = await dbContext.EnrollmentLinks
                .Where(item =>
                    item.Id == firstLink.Value.Source!.EnrollmentLinkId ||
                    item.Id == secondLink.Value.Source!.EnrollmentLinkId)
                .SumAsync(item => item.ReservedClaims);
            Assert.Equal(1, reservedClaims);
        }

        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> unrelatedLink =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 5));
        Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> blockedLink =
            await DispatchAsync(provider, Enrollment(organizationId, Guid.NewGuid(), 5));
        Assert.True(unrelatedLink.IsSuccess, unrelatedLink.Error.Code);
        Assert.True(blockedLink.IsSuccess, blockedLink.Error.Code);

        await using AsyncServiceScope holderScope = provider.CreateAsyncScope();
        OrganizationsDbContext holderDbContext = holderScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await using var holderTransaction = await holderDbContext.Database.BeginTransactionAsync();
        IOrganizationJoinSubjectCoordinator joinSubjects = holderScope.ServiceProvider
            .GetRequiredService<IOrganizationJoinSubjectCoordinator>();
        await joinSubjects.AcquireAsync(
            organizationId,
            "held-member",
            CancellationToken.None);

        Result<OrganizationEnrollmentOutcomeDto> unrelated = await DispatchAsync(
                provider,
                new ClaimOrganizationEnrollmentLinkCommand(
                    Assert.IsType<string>(unrelatedLink.Value.Token),
                    "other-member",
                    "user:other-member"))
            .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(unrelated.IsSuccess, unrelated.Error.Code);

        Task<Result<OrganizationEnrollmentOutcomeDto>> blocked = DispatchAsync(
            provider,
            new ClaimOrganizationEnrollmentLinkCommand(
                Assert.IsType<string>(blockedLink.Value.Token),
                "held-member",
                "user:held-member"));
        await AssertBlockedAsync(blocked);

        await holderTransaction.RollbackAsync();
        Result<OrganizationEnrollmentOutcomeDto> released =
            await blocked.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(released.IsSuccess, released.Error.Code);
    }

    private static async Task VerifyGovernanceCoordinationAsync(
        ServiceProvider provider,
        Guid organizationId,
        long initialOrganizationVersion)
    {
        long organizationVersion = initialOrganizationVersion;

        await using (AsyncServiceScope sharedScope = provider.CreateAsyncScope())
        {
            OrganizationsDbContext dbContext = sharedScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            IRequestDispatcher dispatcher = sharedScope.ServiceProvider
                .GetRequiredService<IRequestDispatcher>();
            Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> issued =
                await dispatcher.SendAsync(
                    Invitation(
                        organizationId,
                        Guid.NewGuid(),
                        "shared-first@example.com"),
                    CancellationToken.None);
            Assert.True(issued.IsSuccess, issued.Error.Code);

            Task<Result<OrganizationDto>> lifecycleTask = DispatchAsync(
                provider,
                new ChangeOrganizationLifecycleCommand(
                    organizationId,
                    Guid.NewGuid(),
                    OrganizationLifecycleAction.Suspend,
                    organizationVersion,
                    "owner",
                    "user:owner"));
            await AssertBlockedAsync(lifecycleTask);

            await transaction.CommitAsync();
            Result<OrganizationDto> suspended = await lifecycleTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(suspended.IsSuccess, suspended.Error.Code);
            organizationVersion = suspended.Value.Version;
        }

        Result<OrganizationDto> reactivated = await DispatchAsync(
            provider,
            new ChangeOrganizationLifecycleCommand(
                organizationId,
                Guid.NewGuid(),
                OrganizationLifecycleAction.Reactivate,
                organizationVersion,
                "owner",
                "user:owner"));
        Assert.True(reactivated.IsSuccess, reactivated.Error.Code);
        organizationVersion = reactivated.Value.Version;

        await using (AsyncServiceScope exclusiveScope = provider.CreateAsyncScope())
        {
            OrganizationsDbContext dbContext = exclusiveScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            IRequestDispatcher dispatcher = exclusiveScope.ServiceProvider
                .GetRequiredService<IRequestDispatcher>();
            Result<OrganizationDto> suspended = await dispatcher.SendAsync(
                new ChangeOrganizationLifecycleCommand(
                    organizationId,
                    Guid.NewGuid(),
                    OrganizationLifecycleAction.Suspend,
                    organizationVersion,
                    "owner",
                    "user:owner"),
                CancellationToken.None);
            Assert.True(suspended.IsSuccess, suspended.Error.Code);

            Task<Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>>> issueTask =
                DispatchAsync(
                    provider,
                    Invitation(
                        organizationId,
                        Guid.NewGuid(),
                        "exclusive-first@example.com"));
            await AssertBlockedAsync(issueTask);

            await transaction.CommitAsync();
            Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> rejected =
                await issueTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(rejected.IsFailure);
            Assert.Equal(OrganizationDomainErrors.OrganizationNotActive.Code, rejected.Error.Code);
            organizationVersion = suspended.Value.Version;
        }

        reactivated = await DispatchAsync(
            provider,
            new ChangeOrganizationLifecycleCommand(
                organizationId,
                Guid.NewGuid(),
                OrganizationLifecycleAction.Reactivate,
                organizationVersion,
                "owner",
                "user:owner"));
        Assert.True(reactivated.IsSuccess, reactivated.Error.Code);

        await using (AsyncServiceScope sharedHolderScope = provider.CreateAsyncScope())
        {
            OrganizationsDbContext dbContext = sharedHolderScope.ServiceProvider
                .GetRequiredService<OrganizationsDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            IOrganizationGovernanceCoordinator governance = sharedHolderScope.ServiceProvider
                .GetRequiredService<IOrganizationGovernanceCoordinator>();
            await governance.AcquireSharedAsync(organizationId, CancellationToken.None);

            Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> parallelShared =
                await DispatchAsync(
                        provider,
                        Invitation(
                            organizationId,
                            Guid.NewGuid(),
                            "parallel-shared@example.com"))
                    .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(parallelShared.IsSuccess, parallelShared.Error.Code);
            await transaction.RollbackAsync();
        }

        Guid otherOrganizationId = Guid.NewGuid();
        Result<OrganizationMembershipSummaryDto> otherOrganization = await DispatchAsync(
            provider,
            new CreateOrganizationCommand(
                otherOrganizationId,
                "Canal House",
                "canal-house",
                "other-owner",
                "user:other-owner"));
        Assert.True(otherOrganization.IsSuccess, otherOrganization.Error.Code);

        await using AsyncServiceScope exclusiveHolderScope = provider.CreateAsyncScope();
        OrganizationsDbContext exclusiveDbContext = exclusiveHolderScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await using var exclusiveTransaction = await exclusiveDbContext.Database.BeginTransactionAsync();
        IOrganizationGovernanceCoordinator exclusiveGovernance = exclusiveHolderScope.ServiceProvider
            .GetRequiredService<IOrganizationGovernanceCoordinator>();
        await exclusiveGovernance.AcquireExclusiveAsync(organizationId, CancellationToken.None);

        Result<OrganizationJoinSourceIssuance<OrganizationInvitationDto>> unrelated =
            await DispatchAsync(
                    provider,
                    new IssueOrganizationInvitationCommand(
                        new OrganizationInvitationIssuanceRequest(
                            Guid.NewGuid(),
                            otherOrganizationId,
                            "unrelated@example.com",
                            24,
                            "other-owner",
                            "user:other-owner")))
                .WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(unrelated.IsSuccess, unrelated.Error.Code);
        await exclusiveTransaction.RollbackAsync();
    }

    private static async Task AssertBlockedAsync(Task task)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        Assert.False(task.IsCompleted, "The conflicting operation did not wait for the transaction lock.");
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["Organizations:SelfServiceCreationEnabled"] = "true",
            ["Organizations:InvitationDefaultLifetimeHours"] = "168",
            ["Organizations:InvitationMaxLifetimeHours"] = "720",
            ["Organizations:EnrollmentDefaultLifetimeHours"] = "24",
            ["Organizations:EnrollmentMaxLifetimeHours"] = "720",
            ["Organizations:EnrollmentMaxClaims"] = "1000",
            ["Organizations:Lifecycle:Enabled"] = "false",
            ["Organizations:Retention:Enabled"] = "false"
        });
        builder.Services.AddSingleton<ISystemClock>(new FixedClock());
        builder.Services.AddSingleton<IIdGenerator, TestIdGenerator>();
        builder.AddCqrsInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddOrganizationsApplication(builder.Configuration);
        builder.AddOrganizationsPersistence();
        return builder.Services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task<Result<TResponse>> DispatchAsync<TResponse>(
        ServiceProvider provider,
        ICommand<TResponse> command)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(command, CancellationToken.None);
    }

    private static IssueOrganizationInvitationCommand Invitation(
        Guid organizationId,
        Guid sourceId,
        string? recipientEmail) =>
        new(new OrganizationInvitationIssuanceRequest(
            sourceId,
            organizationId,
            recipientEmail,
            24,
            "owner",
            "user:owner"));

    private static IssueOrganizationEnrollmentLinkCommand Enrollment(
        Guid organizationId,
        Guid sourceId,
        int maximumClaims) =>
        new(new OrganizationEnrollmentLinkIssuanceRequest(
            sourceId,
            organizationId,
            24,
            maximumClaims,
            OrganizationEnrollmentApprovalMode.RequiresApproval,
            "owner",
            "user:owner"));

    private static ReissueOrganizationInvitationCommand Reissue(
        Guid organizationId,
        OrganizationInvitationDto predecessor,
        Guid replacementSourceId) =>
        new(
            organizationId,
            predecessor.InvitationId,
            replacementSourceId,
            predecessor.Version,
            24,
            "owner",
            "user:owner");

    private static RotateOrganizationEnrollmentLinkCommand Rotate(
        Guid organizationId,
        OrganizationEnrollmentLinkDto predecessor,
        Guid replacementSourceId) =>
        new(
            organizationId,
            predecessor.EnrollmentLinkId,
            replacementSourceId,
            predecessor.Version,
            24,
            "owner",
            "user:owner");

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
