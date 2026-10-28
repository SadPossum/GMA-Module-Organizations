namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Persistence;
using Gma.Modules.Organizations.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationInvitationInspectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Inspector_returns_one_exact_effective_status_without_tracking_it()
    {
        Guid organizationId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        OrganizationInvitation invitation = CreateInvitation(
            organizationId,
            invitationId,
            Now.AddHours(1));
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(Now));

        OrganizationInvitationStatus? found = await inspector.FindStatusAsync(
            organizationId,
            invitationId);

        Assert.Equal(OrganizationInvitationStatus.Pending, found);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_maps_pending_invitation_to_expired_at_the_exact_boundary()
    {
        Guid organizationId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        DateTimeOffset expiresAtUtc = Now.AddHours(1);
        OrganizationInvitation invitation = CreateInvitation(
            organizationId,
            invitationId,
            expiresAtUtc);
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(expiresAtUtc));

        OrganizationInvitationStatus? found = await inspector.FindStatusAsync(
            organizationId,
            invitationId);

        Assert.Equal(OrganizationInvitationStatus.Expired, found);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_does_not_cross_organization_or_invitation_keys()
    {
        Guid organizationId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        OrganizationInvitation invitation = CreateInvitation(
            organizationId,
            invitationId,
            Now.AddHours(1));
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(Now));

        Assert.Null(await inspector.FindStatusAsync(
            Guid.NewGuid(),
            invitationId));
        Assert.Null(await inspector.FindStatusAsync(
            organizationId,
            Guid.NewGuid()));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_returns_terminal_status_while_retained_and_null_after_removal()
    {
        Guid organizationId = Guid.NewGuid();
        Guid invitationId = Guid.NewGuid();
        DateTimeOffset expiresAtUtc = Now.AddHours(1);
        OrganizationInvitation invitation = CreateInvitation(
            organizationId,
            invitationId,
            expiresAtUtc);
        Assert.True(invitation.Revoke(
            invitation.Version,
            "subject-owner",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        await using OrganizationsDbContext dbContext = CreateDbContext();
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(expiresAtUtc.AddHours(1)));

        Assert.Equal(
            OrganizationInvitationStatus.Revoked,
            await inspector.FindStatusAsync(organizationId, invitationId));

        dbContext.Invitations.Remove(invitation);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Assert.Null(await inspector.FindStatusAsync(organizationId, invitationId));
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Inspector_rejects_invalid_keys_before_using_dependencies()
    {
        OrganizationsDbContext dbContext = CreateDbContext();
        await dbContext.DisposeAsync();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new ThrowingClock());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindStatusAsync(Guid.Empty, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            inspector.FindStatusAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task Inspector_propagates_query_failures()
    {
        OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(Now));
        await dbContext.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            inspector.FindStatusAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Inspector_propagates_query_cancellation()
    {
        await using OrganizationsDbContext dbContext = CreateDbContext();
        OrganizationInvitationInspector inspector = new(
            dbContext,
            new FixedClock(Now));
        CancellationToken cancellationToken = new(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            inspector.FindStatusAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                cancellationToken));
    }

    [Fact]
    public void Persistence_composition_registers_the_invitation_inspector()
    {
        HostApplicationBuilder builder = new(new HostApplicationBuilderSettings
        {
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] =
                "Server=localhost;Database=organizations-tests;Integrated Security=true;TrustServerCertificate=true"
        });

        builder.AddOrganizationsPersistence();

        Assert.Contains(builder.Services, descriptor =>
            descriptor.ServiceType == typeof(IOrganizationInvitationInspector) &&
            descriptor.ImplementationType == typeof(OrganizationInvitationInspector) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static OrganizationInvitation CreateInvitation(
        Guid organizationId,
        Guid invitationId,
        DateTimeOffset expiresAtUtc) => OrganizationInvitation.Create(
            invitationId,
            organizationId,
            "subject-owner",
            recipientEmail: null,
            new string('a', OrganizationInvitation.TokenDigestLength),
            expiresAtUtc,
            "subject-owner",
            Guid.NewGuid(),
            Now).Value;

    private static OrganizationsDbContext CreateDbContext()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase($"organizations-invitation-inspector-{Guid.NewGuid():N}")
                .Options;
        return new OrganizationsDbContext(options);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class ThrowingClock : ISystemClock
    {
        public DateTimeOffset UtcNow => throw new InvalidOperationException(
            "The clock must not be read for an invalid key.");
    }
}
