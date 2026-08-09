namespace Gma.Modules.Organizations.Tests.Persistence;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationsPersistenceRetryBehaviorTests
{
    [Theory]
    [InlineData(2601, null)]
    [InlineData(2627, null)]
    [InlineData(null, "23505")]
    public void Unique_constraint_codes_are_provider_neutral(int? sqlServerError, string? postgreSqlState)
    {
        Assert.True(OrganizationsUniqueConstraintDetector.IsUniqueViolation(sqlServerError, postgreSqlState));
    }

    [Fact]
    public async Task Retryable_persistence_conflict_clears_tracking_and_reexecutes_once()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using OrganizationsDbContext dbContext = new(options);
        OrganizationsPersistenceRetryBehavior<CreateOrganizationCommand, OrganizationMembershipSummaryDto> behavior =
            new(dbContext, _ => true);
        CreateOrganizationCommand command = new(
            Guid.NewGuid(), "Harbor House", "harbor-house", "member-a", "user:member-a");
        int attempts = 0;

        async Task<Result<OrganizationMembershipSummaryDto>> Next()
        {
            attempts++;
            if (attempts == 1)
            {
                dbContext.Organizations.Add(Organization.Create(
                    Guid.NewGuid(), "Harbor House", "harbor-house",
                    "user:member-a", Guid.NewGuid(), DateTimeOffset.UtcNow).Value);
                throw new DbUpdateException("simulated unique conflict");
            }

            Assert.Empty(dbContext.ChangeTracker.Entries());
            await Task.CompletedTask;
            return Result.Failure<OrganizationMembershipSummaryDto>(
                OrganizationApplicationErrors.SlugConflict);
        }

        Result<OrganizationMembershipSummaryDto> result = await behavior.HandleAsync(
            command, Next, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(OrganizationApplicationErrors.SlugConflict, result.Error);
    }

    [Fact]
    public async Task Translated_concurrency_conflict_clears_tracking_and_reexecutes_once()
    {
        DbContextOptions<OrganizationsDbContext> options =
            new DbContextOptionsBuilder<OrganizationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        await using OrganizationsDbContext dbContext = new(options);
        OrganizationsPersistenceRetryBehavior<CreateOrganizationCommand, OrganizationMembershipSummaryDto> behavior =
            new(dbContext, _ => false);
        CreateOrganizationCommand command = new(
            Guid.NewGuid(), "Harbor House", "harbor-house", "member-a", "user:member-a");
        int attempts = 0;

        Task<Result<OrganizationMembershipSummaryDto>> Next()
        {
            attempts++;
            if (attempts == 1)
            {
                throw new OptimisticConcurrencyException(
                    OrganizationsModuleMetadata.Name,
                    new DbUpdateConcurrencyException("simulated concurrency conflict"));
            }

            return Task.FromResult(Result.Failure<OrganizationMembershipSummaryDto>(
                OrganizationApplicationErrors.SlugConflict));
        }

        Result<OrganizationMembershipSummaryDto> result = await behavior.HandleAsync(
            command, Next, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(OrganizationApplicationErrors.SlugConflict, result.Error);
    }
}
