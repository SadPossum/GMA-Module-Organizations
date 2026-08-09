namespace Gma.Modules.Organizations.Tests.Support;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Domain.Aggregates;

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
