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
