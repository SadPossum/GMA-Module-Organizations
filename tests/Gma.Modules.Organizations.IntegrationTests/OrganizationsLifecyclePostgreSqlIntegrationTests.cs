namespace Gma.Modules.Organizations.IntegrationTests;

using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.IntegrationTests.Support;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;
using ClaimExpiredIntegrationEvent =
    Gma.Modules.Organizations.Contracts.OrganizationEnrollmentClaimExpiredIntegrationEvent;
using InvitationExpiredIntegrationEvent =
    Gma.Modules.Organizations.Contracts.OrganizationInvitationExpiredIntegrationEvent;
using LinkExpiredIntegrationEvent =
    Gma.Modules.Organizations.Contracts.OrganizationEnrollmentLinkExpiredIntegrationEvent;

[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public sealed class OrganizationsLifecyclePostgreSqlIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Lifecycle_is_transactional_concurrency_safe_and_publishes_payload_free_expiry_facts()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("organizations_lifecycle_tests")
                .Build();
        await postgreSql.StartAsync();
        await using ServiceProvider provider = CreateProvider(postgreSql.GetConnectionString());
        await MigrateAsync(provider);

        (Guid invitationId, Guid linkId, Guid claimId) = await SeedDueRecordsAsync(provider);

        Task<Result<int>> firstInvitationExpiry = DispatchAsync(
            provider, new ExpireOrganizationInvitationsCommand(10));
        Task<Result<int>> secondInvitationExpiry = DispatchAsync(
            provider, new ExpireOrganizationInvitationsCommand(10));
        Result<int>[] invitationResults = await Task.WhenAll(
            firstInvitationExpiry, secondInvitationExpiry);
        Result<int> claimResult = await DispatchAsync(
            provider, new ExpireOrganizationEnrollmentClaimsCommand(10));
        Result<int> linkResult = await DispatchAsync(
            provider, new ExpireOrganizationEnrollmentLinksCommand(10));

        Assert.All(invitationResults, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, invitationResults.Sum(result => result.Value));
        Assert.True(claimResult.IsSuccess);
        Assert.Equal(1, claimResult.Value);
        Assert.True(linkResult.IsSuccess);
        Assert.Equal(1, linkResult.Value);

        await VerifyStoredLifecycleAsync(provider, invitationId, linkId, claimId);
    }

    private static async Task<(Guid InvitationId, Guid LinkId, Guid ClaimId)> SeedDueRecordsAsync(
        ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Lifecycle House", "lifecycle-house",
            "user:owner", Guid.NewGuid(), Now.AddDays(-2)).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", OrganizationMembershipRole.Owner,
            "user:owner", Guid.NewGuid(), Now.AddDays(-2)).Value;
        OrganizationInvitation invitation = OrganizationInvitation.Create(
            Guid.NewGuid(), organization.Id, "owner", "invitee@example.test",
            new string('e', 64), Now.AddDays(-1), "user:owner",
            Guid.NewGuid(), Now.AddDays(-2)).Value;
        OrganizationEnrollmentLink link = OrganizationEnrollmentLink.Create(
            Guid.NewGuid(), organization.Id, "owner", new string('f', 64),
            Now.AddDays(-1), 1, OrganizationEnrollmentApprovalMode.RequiresApproval,
            "user:owner", Guid.NewGuid(), Now.AddDays(-2)).Value;
        Assert.True(link.ReserveClaim(
            "user:member", Guid.NewGuid(), Now.AddDays(-2).AddHours(1)).IsSuccess);
        OrganizationEnrollmentClaim claim = OrganizationEnrollmentClaim.Create(
            Guid.NewGuid(), organization.Id, link.Id, "member",
            OrganizationEnrollmentClaimState.Pending, null,
            "user:member", Guid.NewGuid(), Now.AddDays(-2).AddHours(1),
            Now.AddHours(-1)).Value;
        dbContext.AddRange(organization, owner, invitation, link, claim);
        await dbContext.SaveChangesAsync();
        return (invitation.Id, link.Id, claim.Id);
    }

    private static async Task VerifyStoredLifecycleAsync(
        ServiceProvider provider,
        Guid invitationId,
        Guid linkId,
        Guid claimId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        OrganizationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        Assert.Equal(
            OrganizationInvitationState.Expired,
            (await dbContext.Invitations.SingleAsync(item => item.Id == invitationId)).Status);
        Assert.Equal(
            OrganizationEnrollmentClaimState.Expired,
            (await dbContext.EnrollmentClaims.SingleAsync(item => item.Id == claimId)).Status);
        OrganizationEnrollmentLink storedLink =
            await dbContext.EnrollmentLinks.SingleAsync(item => item.Id == linkId);
        Assert.Equal(OrganizationEnrollmentLinkState.Expired, storedLink.Status);
        Assert.Equal(0, storedLink.ReservedClaims);

        var outbox = await dbContext.OutboxMessages.AsNoTracking().ToArrayAsync();
        Assert.Contains(outbox, message =>
            message.EventType == typeof(InvitationExpiredIntegrationEvent).FullName);
        Assert.Contains(outbox, message =>
            message.EventType == typeof(ClaimExpiredIntegrationEvent).FullName);
        Assert.Contains(outbox, message =>
            message.EventType == typeof(LinkExpiredIntegrationEvent).FullName);
        Assert.DoesNotContain(outbox, message =>
            message.EventType.EndsWith("ExpiredIntegrationEvent", StringComparison.Ordinal) &&
            (message.Payload.Contains("invitee@example.test", StringComparison.Ordinal) ||
             message.Payload.Contains("\"subjectId\":\"member\"", StringComparison.OrdinalIgnoreCase)));
    }

    private static ServiceProvider CreateProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["Organizations:EnrollmentClaimLifetimeHours"] = "168",
            ["Organizations:Lifecycle:Enabled"] = "false",
            ["Organizations:Retention:Enabled"] = "false"
        });
        builder.Services.AddSingleton<ISystemClock>(new FixedClock(Now));
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
        OrganizationsDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task<Result<int>> DispatchAsync(
        ServiceProvider provider,
        ICommand<int> command)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        return await dispatcher.SendAsync(command, CancellationToken.None);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
