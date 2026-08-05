namespace Gma.Modules.Organizations.Persistence;

using System.Data;
using Gma.Framework.Runtime.Maintenance;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        IQueryable<RetentionCandidate> candidates = dbContext.Invitations
            .AsNoTracking()
            .Where(invitation =>
                invitation.Status != OrganizationInvitationState.Pending &&
                invitation.LastChangedAtUtc <= cutoffUtc &&
                !dbContext.OrganizationScopeStates.Any(state =>
                    state.OrganizationId == invitation.OrganizationId &&
                    state.IsClosed))
            .OrderBy(invitation => invitation.LastChangedAtUtc)
            .ThenBy(invitation => invitation.Id)
            .Select(invitation => new RetentionCandidate(
                invitation.Id,
                invitation.OrganizationId));
        return await DeleteRevisionFencedBatchAsync(
                dbContext,
                candidates,
                batchSize,
                (ids, token) => dbContext.Invitations
                    .Where(invitation => ids.Contains(invitation.Id))
                    .ExecuteDeleteAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> DeleteResolvedEnrollmentClaimsBatchAsync(
        OrganizationsDbContext dbContext,
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        IQueryable<RetentionCandidate> candidates =
            from claim in dbContext.EnrollmentClaims.AsNoTracking()
            join link in dbContext.EnrollmentLinks.AsNoTracking()
                on claim.EnrollmentLinkId equals link.Id
            where claim.Status != OrganizationEnrollmentClaimState.Pending &&
                  link.Status != OrganizationEnrollmentLinkState.Active &&
                  link.LastChangedAtUtc <= cutoffUtc &&
                  !dbContext.OrganizationScopeStates.Any(state =>
                      state.OrganizationId == claim.OrganizationId &&
                      state.IsClosed)
            orderby claim.LastChangedAtUtc, claim.Id
            select new RetentionCandidate(claim.Id, claim.OrganizationId);
        return await DeleteRevisionFencedBatchAsync(
                dbContext,
                candidates,
                batchSize,
                (ids, token) => dbContext.EnrollmentClaims
                    .Where(claim => ids.Contains(claim.Id))
                    .ExecuteDeleteAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> DeleteEnrollmentLinksBatchAsync(
        OrganizationsDbContext dbContext,
        DateTimeOffset cutoffUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        IQueryable<RetentionCandidate> candidates = dbContext.EnrollmentLinks
            .AsNoTracking()
            .Where(link =>
                link.Status != OrganizationEnrollmentLinkState.Active &&
                link.LastChangedAtUtc <= cutoffUtc &&
                !dbContext.EnrollmentClaims.Any(claim =>
                    claim.EnrollmentLinkId == link.Id) &&
                !dbContext.OrganizationScopeStates.Any(state =>
                    state.OrganizationId == link.OrganizationId &&
                    state.IsClosed))
            .OrderBy(link => link.LastChangedAtUtc)
            .ThenBy(link => link.Id)
            .Select(link => new RetentionCandidate(
                link.Id,
                link.OrganizationId));
        return await DeleteRevisionFencedBatchAsync(
                dbContext,
                candidates,
                batchSize,
                (ids, token) => dbContext.EnrollmentLinks
                    .Where(link => ids.Contains(link.Id))
                    .ExecuteDeleteAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<int> DeleteRevisionFencedBatchAsync(
        OrganizationsDbContext dbContext,
        IQueryable<RetentionCandidate> candidates,
        int batchSize,
        Func<Guid[], CancellationToken, Task<int>> delete,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction =
            dbContext.Database.IsRelational() &&
            dbContext.Database.CurrentTransaction is null
                ? await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken)
                    .ConfigureAwait(false)
                : null;
        RetentionCandidate[] selected = await candidates
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selected.Length == 0)
        {
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return 0;
        }

        Guid[] organizationIds = selected
            .Select(candidate => candidate.OrganizationId)
            .Distinct()
            .ToArray();
        if (!await dbContext.TryRegisterMaintenanceScopeMutationsAsync(
                organizationIds,
                cancellationToken).ConfigureAwait(false))
        {
            throw new OrganizationScopeClosedException();
        }

        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] ids = selected.Select(candidate => candidate.Id).ToArray();
        int removed = await delete(ids, cancellationToken)
            .ConfigureAwait(false);
        if (removed != selected.Length)
        {
            throw new InvalidDataException(
                "Organization retention changed during its revision fence.");
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return removed;
    }

    private sealed record RetentionCandidate(Guid Id, Guid OrganizationId);
}
