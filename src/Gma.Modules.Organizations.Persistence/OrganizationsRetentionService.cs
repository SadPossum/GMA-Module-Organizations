namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Runtime.Maintenance;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class OrganizationsRetentionService(
    IServiceScopeFactory scopeFactory,
    ISystemClock clock,
    IOptions<OrganizationsRetentionOptions> options,
    ILogger<OrganizationsRetentionService> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogIterationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogIterationFailed)),
            "Organizations retention iteration failed with {ExceptionType}; cleanup will retry on the next interval.");

    private static readonly Action<ILogger, int, int, int, Exception?> LogCleanupCompleted =
        LoggerMessage.Define<int, int, int>(
            LogLevel.Information,
            new EventId(2, nameof(LogCleanupCompleted)),
            "Organizations retention removed {InvitationCount} invitations, {ClaimCount} resolved enrollment claims, and {LinkCount} enrollment links.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(options.Value.IntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogIterationFailed(logger, exception.GetType().Name, null);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        OrganizationsRetentionOptions settings = options.Value;
        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset invitationCutoffUtc = nowUtc.AddDays(-settings.InvitationHistoryDays);
        DateTimeOffset enrollmentCutoffUtc = nowUtc.AddDays(-settings.EnrollmentHistoryDays);

        int invitationCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => DeleteInvitationsBatchAsync(
                    dbContext, invitationCutoffUtc, batchSize, token),
                cancellationToken)
            .ConfigureAwait(false);
        int claimCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => DeleteResolvedEnrollmentClaimsBatchAsync(
                    dbContext, enrollmentCutoffUtc, batchSize, token),
                cancellationToken)
            .ConfigureAwait(false);
        int linkCount = await BoundedBatchProcessor.ExecuteAsync(
                settings.BatchSize,
                settings.MaxBatchesPerCategoryPerCycle,
                (batchSize, token) => DeleteEnrollmentLinksBatchAsync(
                    dbContext, enrollmentCutoffUtc, batchSize, token),
                cancellationToken)
            .ConfigureAwait(false);

        if (invitationCount > 0 || claimCount > 0 || linkCount > 0)
        {
            LogCleanupCompleted(logger, invitationCount, claimCount, linkCount, null);
        }
    }

    private static async Task<int> DeleteInvitationsBatchAsync(
        OrganizationsDbContext dbContext,
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        Guid[] invitationIds = await dbContext.Invitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.Status != OrganizationInvitationState.Pending &&
                invitation.LastChangedAtUtc <= cutoffUtc)
            .OrderBy(invitation => invitation.LastChangedAtUtc)
            .ThenBy(invitation => invitation.Id)
            .Select(invitation => invitation.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return invitationIds.Length == 0
            ? 0
            : await dbContext.Invitations
                .Where(invitation => invitationIds.Contains(invitation.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<int> DeleteResolvedEnrollmentClaimsBatchAsync(
        OrganizationsDbContext dbContext,
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        Guid[] claimIds = await (
            from claim in dbContext.EnrollmentClaims.AsNoTracking()
            join link in dbContext.EnrollmentLinks.AsNoTracking()
                on claim.EnrollmentLinkId equals link.Id
            where claim.Status != OrganizationEnrollmentClaimState.Pending &&
                  link.Status != OrganizationEnrollmentLinkState.Active &&
                  link.LastChangedAtUtc <= cutoffUtc
            orderby claim.LastChangedAtUtc, claim.Id
            select claim.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimIds.Length == 0
            ? 0
            : await dbContext.EnrollmentClaims
                .Where(claim => claimIds.Contains(claim.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<int> DeleteEnrollmentLinksBatchAsync(
        OrganizationsDbContext dbContext,
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        Guid[] linkIds = await dbContext.EnrollmentLinks
            .AsNoTracking()
            .Where(link =>
                link.Status != OrganizationEnrollmentLinkState.Active &&
                link.LastChangedAtUtc <= cutoffUtc &&
                !dbContext.EnrollmentClaims.Any(claim => claim.EnrollmentLinkId == link.Id))
            .OrderBy(link => link.LastChangedAtUtc)
            .ThenBy(link => link.Id)
            .Select(link => link.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return linkIds.Length == 0
            ? 0
            : await dbContext.EnrollmentLinks
                .Where(link => linkIds.Contains(link.Id))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
    }
}
