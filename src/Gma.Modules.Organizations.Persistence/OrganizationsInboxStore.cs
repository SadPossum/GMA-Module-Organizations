namespace Gma.Modules.Organizations.Persistence;

using System.Data;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal sealed class OrganizationsInboxStore(OrganizationsDbContext dbContext, ISystemClock clock, IIdGenerator idGenerator)
    : EfInboxStore<OrganizationsDbContext>(
        dbContext,
        clock,
        idGenerator,
        OrganizationsMigrations.Schema)
{
    protected override ValueTask<bool> IsAdmittedAsync(
        InboxMessageRecord message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.ScopeId))
        {
            return ValueTask.FromResult(true);
        }

        return OrganizationMessageScopes.TryParse(
            message.ScopeId,
            out Guid organizationId)
            ? this.DbContext.TryRegisterScopeMutationAsync(
                organizationId,
                cancellationToken)
            : ValueTask.FromResult(false);
    }

    public override async Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset processedBeforeUtc,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        ValidateCleanupArguments(processedBeforeUtc, maxMessages);
        await using IDbContextTransaction? transaction =
            await this.BeginSerializableTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
        IQueryable<InboxMessage> candidates = this.DbContext.InboxMessages
            .Where(message =>
                message.Status == InboxMessageStatus.Processed &&
                message.ProcessedAtUtc != null &&
                message.ProcessedAtUtc < processedBeforeUtc)
            .Where(message =>
                message.ScopeId == null ||
                !this.DbContext.OrganizationScopeStates.Any(state =>
                    state.ScopeId == message.ScopeId && state.IsClosed))
            .OrderBy(message => message.ProcessedAtUtc)
            .ThenBy(message => message.Id)
            .ThenBy(message => message.Handler)
            .Take(maxMessages);
        InboxCleanupCandidate[] selected = await candidates
            .Select(message => new InboxCleanupCandidate(
                message.Id,
                message.Handler,
                message.ScopeId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (selected.Length == 0)
        {
            await CommitAsync(transaction, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        Guid[] organizationIds = selected
            .Select(candidate => candidate.ScopeId)
            .Where(scopeId => OrganizationMessageScopes.TryParse(
                scopeId,
                out _))
            .Select(scopeId => Guid.Parse(scopeId!))
            .Distinct()
            .ToArray();
        if (organizationIds.Length > 0)
        {
            if (!await this.DbContext
                    .TryRegisterMaintenanceScopeMutationsAsync(
                        organizationIds,
                        cancellationToken).ConfigureAwait(false))
            {
                throw new OrganizationScopeClosedException();
            }

            await this.DbContext.SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int removed = await candidates
            .Take(selected.Length)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        if (removed != selected.Length)
        {
            throw new InvalidDataException(
                "Organization inbox cleanup changed during its revision fence.");
        }

        await CommitAsync(transaction, cancellationToken)
            .ConfigureAwait(false);
        return removed;
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!this.DbContext.Database.IsRelational() ||
            this.DbContext.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await this.DbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CommitAsync(
        IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static void ValidateCleanupArguments(
        DateTimeOffset processedBeforeUtc,
        int maxMessages)
    {
        if (processedBeforeUtc == default)
        {
            throw new ArgumentException(
                $"{nameof(processedBeforeUtc)} must not be the default timestamp.",
                nameof(processedBeforeUtc));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
    }

    private sealed record InboxCleanupCandidate(
        Guid Id,
        string Handler,
        string? ScopeId);
}
