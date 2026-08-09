namespace Gma.Modules.Organizations.Tests.Support;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.Extensions.DependencyInjection;

internal sealed class TestClock(DateTimeOffset nowUtc) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = nowUtc;
}

internal sealed class TestIds : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}

internal sealed class TestOrganizationJoinSourceIssuanceCoordinator(
    IOrganizationRepository organizations) : IOrganizationJoinSourceIssuanceCoordinator
{
    public Task<OrganizationInvitation?> AcquireInvitationAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        organizations.GetInvitationAsync(organizationId, sourceId, cancellationToken);

    public Task<OrganizationEnrollmentLink?> AcquireEnrollmentLinkAsync(
        Guid organizationId,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        organizations.GetEnrollmentLinkAsync(organizationId, sourceId, cancellationToken);

    public Task AcquireReplacementAsync(
        Guid sourceId,
        Guid replacementSourceId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}

internal enum TestOrganizationGovernanceMode
{
    Shared,
    Exclusive
}

internal sealed class TestOrganizationGovernanceCoordinator(
    Action<Guid, TestOrganizationGovernanceMode>? onAcquire = null)
    : IOrganizationGovernanceCoordinator
{
    private readonly Lock sync = new();

    public List<(Guid OrganizationId, TestOrganizationGovernanceMode Mode)> Acquisitions { get; } = [];

    public Task AcquireSharedAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            organizationId,
            TestOrganizationGovernanceMode.Shared,
            cancellationToken);

    public Task AcquireExclusiveAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        this.AcquireAsync(
            organizationId,
            TestOrganizationGovernanceMode.Exclusive,
            cancellationToken);

    private Task AcquireAsync(
        Guid organizationId,
        TestOrganizationGovernanceMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.Acquisitions.Add((organizationId, mode));
        }

        onAcquire?.Invoke(organizationId, mode);
        return Task.CompletedTask;
    }
}

internal static class TestOrganizationGovernanceRegistration
{
    public static IServiceCollection AddTestOrganizationGovernance(
        this IServiceCollection services)
    {
        services.AddSingleton<IOrganizationGovernanceCoordinator>(
            new TestOrganizationGovernanceCoordinator());
        return services;
    }
}
