namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
public sealed class OrganizationsDbContext(DbContextOptions<OrganizationsDbContext> options)
    : DbContext(options)
{
    private const int ScopeStateQueryBatchSize = 500;

    public DbSet<Organization> Organizations => this.Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => this.Set<OrganizationMembership>();
    public DbSet<OrganizationInvitation> Invitations => this.Set<OrganizationInvitation>();
    public DbSet<OrganizationEnrollmentLink> EnrollmentLinks => this.Set<OrganizationEnrollmentLink>();
    public DbSet<OrganizationEnrollmentClaim> EnrollmentClaims => this.Set<OrganizationEnrollmentClaim>();
    public DbSet<OrganizationScopeState> OrganizationScopeStates =>
        this.Set<OrganizationScopeState>();
    public DbSet<OrganizationScopeDestroyOperation>
        OrganizationScopeDestroyOperations =>
        this.Set<OrganizationScopeDestroyOperation>();
    public DbSet<OrganizationScopeDestroyReceipt>
        OrganizationScopeDestroyReceipts =>
        this.Set<OrganizationScopeDestroyReceipt>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(OrganizationsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationsDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.RegisterTrackedScopeMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await this.RegisterTrackedScopeMutationsAsync(cancellationToken)
            .ConfigureAwait(false);
        return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal async ValueTask<bool> TryRegisterScopeMutationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await this.TryRegisterScopeMutationsAsync(
                [organizationId],
                cancellationToken)
            .ConfigureAwait(false);

    internal Task<bool> TryRegisterMaintenanceScopeMutationsAsync(
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken) =>
        this.TryRegisterScopeMutationsAsync(
            organizationIds,
            cancellationToken);

    internal async Task<int> SaveScopeDestructionChangesAsync(
        Guid organizationId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        this.ChangeTracker.DetectChanges();
        OrganizationScopeState? state = this.OrganizationScopeStates.Local
            .SingleOrDefault(candidate =>
                candidate.OrganizationId == organizationId);
        if (organizationId == Guid.Empty ||
            operationId == Guid.Empty ||
            state is null ||
            !state.IsClosed ||
            state.CloseOperationId != operationId)
        {
            throw new InvalidOperationException(
                "Organization scope destruction state is unavailable.");
        }

        foreach (var entry in this.ChangeTracker.Entries().Where(entry =>
                     entry.State is EntityState.Added or
                         EntityState.Modified or EntityState.Deleted))
        {
            bool allowed = entry.Entity switch
            {
                OrganizationScopeState candidate =>
                    entry.State is EntityState.Added or EntityState.Modified &&
                    candidate.OrganizationId == organizationId &&
                    candidate.IsClosed &&
                    candidate.CloseOperationId == operationId,
                OrganizationScopeDestroyOperation operation =>
                    operation.OrganizationId == organizationId &&
                    operation.OperationId == operationId,
                OrganizationScopeDestroyReceipt receipt =>
                    entry.State == EntityState.Added &&
                    receipt.OrganizationId == organizationId &&
                    receipt.OperationId == operationId,
                _ => entry.State == EntityState.Deleted &&
                    DestructionOrganizationId(entry.Entity) == organizationId
            };
            if (!allowed)
            {
                throw new InvalidOperationException(
                    "Organization scope destruction attempted an invalid write.");
            }
        }

        return await base.SaveChangesAsync(
                acceptAllChangesOnSuccess: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void RegisterTrackedScopeMutations()
    {
        Guid[] organizationIds = this.ChangedOrganizationIds();
        Dictionary<Guid, OrganizationScopeState> states =
            this.LoadScopeStates(organizationIds);
        foreach (Guid organizationId in organizationIds)
        {
            if (!states[organizationId].RegisterMutation())
            {
                throw new OrganizationScopeClosedException();
            }
        }
    }

    private async Task RegisterTrackedScopeMutationsAsync(
        CancellationToken cancellationToken)
    {
        if (!await this.TryRegisterScopeMutationsAsync(
                this.ChangedOrganizationIds(),
                cancellationToken).ConfigureAwait(false))
        {
            throw new OrganizationScopeClosedException();
        }
    }

    private Guid[] ChangedOrganizationIds() =>
        this.ChangeTracker.Entries()
            .Where(entry => entry.State is
                EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => OrganizationId(entry.Entity))
            .Where(organizationId => organizationId.HasValue)
            .Select(organizationId => organizationId!.Value)
            .Distinct()
            .Order()
            .ToArray();

    private static Guid? OrganizationId(object entity) =>
        entity switch
        {
            OrganizationScopeState or
            OrganizationScopeDestroyOperation or
            OrganizationScopeDestroyReceipt or
            InboxMessage => null,
            Organization organization => organization.Id,
            OrganizationMembership membership => membership.OrganizationId,
            OrganizationInvitation invitation => invitation.OrganizationId,
            OrganizationEnrollmentLink link => link.OrganizationId,
            OrganizationEnrollmentClaim claim => claim.OrganizationId,
            OutboxMessage message => ParseOrganizationId(message.ScopeId),
            _ => null
        };

    private static Guid? DestructionOrganizationId(object entity) =>
        entity switch
        {
            Organization organization => organization.Id,
            OrganizationMembership membership => membership.OrganizationId,
            OrganizationInvitation invitation => invitation.OrganizationId,
            OrganizationEnrollmentLink link => link.OrganizationId,
            OrganizationEnrollmentClaim claim => claim.OrganizationId,
            InboxMessage message => ParseOrganizationId(message.ScopeId),
            OutboxMessage message => ParseOrganizationId(message.ScopeId),
            _ => null
        };

    private Dictionary<Guid, OrganizationScopeState> LoadScopeStates(
        IReadOnlyCollection<Guid> organizationIds)
    {
        Dictionary<Guid, OrganizationScopeState> states =
            this.OrganizationScopeStates.Local
                .Where(state => organizationIds.Contains(state.OrganizationId))
                .ToDictionary(state => state.OrganizationId);
        Guid[] missingOrganizationIds = organizationIds
            .Where(organizationId => !states.ContainsKey(organizationId))
            .ToArray();
        foreach (Guid[] batch in missingOrganizationIds.Chunk(
                     ScopeStateQueryBatchSize))
        {
            foreach (OrganizationScopeState state in this.OrganizationScopeStates
                         .Where(candidate => batch.Contains(
                             candidate.OrganizationId)))
            {
                states.Add(state.OrganizationId, state);
            }
        }

        foreach (Guid organizationId in organizationIds.Where(organizationId =>
                     !states.ContainsKey(organizationId)))
        {
            OrganizationScopeState state =
                OrganizationScopeState.Create(organizationId).Value;
            this.OrganizationScopeStates.Add(state);
            states.Add(organizationId, state);
        }

        return states;
    }

    private async Task<bool> TryRegisterScopeMutationsAsync(
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);
        Guid[] normalizedOrganizationIds = organizationIds
            .Where(organizationId => organizationId != Guid.Empty)
            .Distinct()
            .Order()
            .ToArray();
        if (normalizedOrganizationIds.Length == 0)
        {
            return true;
        }

        Dictionary<Guid, OrganizationScopeState> states =
            this.OrganizationScopeStates.Local
                .Where(state => normalizedOrganizationIds.Contains(
                    state.OrganizationId))
                .ToDictionary(state => state.OrganizationId);
        Guid[] missingOrganizationIds = normalizedOrganizationIds
            .Where(organizationId => !states.ContainsKey(organizationId))
            .ToArray();
        foreach (Guid[] batch in missingOrganizationIds.Chunk(
                     ScopeStateQueryBatchSize))
        {
            OrganizationScopeState[] loaded = await this
                .OrganizationScopeStates
                .Where(state => batch.Contains(state.OrganizationId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (OrganizationScopeState state in loaded)
            {
                states.Add(state.OrganizationId, state);
            }
        }

        foreach (Guid organizationId in normalizedOrganizationIds.Where(
                     organizationId => !states.ContainsKey(organizationId)))
        {
            OrganizationScopeState state =
                OrganizationScopeState.Create(organizationId).Value;
            await this.OrganizationScopeStates.AddAsync(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
            states.Add(organizationId, state);
        }

        if (states.Values.Any(state =>
                state.IsClosed || state.Version == long.MaxValue))
        {
            return false;
        }

        foreach (OrganizationScopeState state in states.Values)
        {
            if (!state.RegisterMutation())
            {
                throw new InvalidOperationException(
                    "Organization scope mutation state changed unexpectedly.");
            }
        }

        return true;
    }

    private static Guid? ParseOrganizationId(string? scopeId) =>
        Guid.TryParseExact(scopeId, "D", out Guid organizationId) &&
        organizationId != Guid.Empty
            ? organizationId
            : null;
}
