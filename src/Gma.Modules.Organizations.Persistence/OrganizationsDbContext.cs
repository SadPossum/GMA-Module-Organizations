namespace Gma.Modules.Organizations.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Modules.Organizations.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
public sealed class OrganizationsDbContext(DbContextOptions<OrganizationsDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => this.Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => this.Set<OrganizationMembership>();
    public DbSet<OrganizationInvitation> Invitations => this.Set<OrganizationInvitation>();
    public DbSet<OrganizationEnrollmentLink> EnrollmentLinks => this.Set<OrganizationEnrollmentLink>();
    public DbSet<OrganizationEnrollmentClaim> EnrollmentClaims => this.Set<OrganizationEnrollmentClaim>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(OrganizationsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationsDbContext).Assembly);
    }
}
