namespace Gma.Modules.Organizations.Tests.Application;

using Gma.Framework.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DomainMembershipRole = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipRole;
using DomainMembershipState = Gma.Modules.Organizations.Domain.Enums.OrganizationMembershipState;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OrganizationMembershipLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Member_state_changes_are_idempotent_and_recover_removed_members()
    {
        TestOrganizationRepository repository = CreateRepository();
        OrganizationMembership member = repository.Memberships.Single(item => item.Role == DomainMembershipRole.Member);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationMembershipStateCommand,
            OrganizationMembershipLifecycleResult>>();

        Result<OrganizationMembershipLifecycleResult> suspended = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                member.OrganizationId,
                member.SubjectId,
                OrganizationMembershipStatus.Suspended,
                "system:membership-sync"),
            CancellationToken.None);
        Result<OrganizationMembershipLifecycleResult> replay = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                member.OrganizationId,
                member.SubjectId,
                OrganizationMembershipStatus.Suspended,
                "system:membership-sync"),
            CancellationToken.None);
        Result<OrganizationMembershipLifecycleResult> removed = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                member.OrganizationId,
                member.SubjectId,
                OrganizationMembershipStatus.Removed,
                "system:membership-sync"),
            CancellationToken.None);
        Result<OrganizationMembershipLifecycleResult> restored = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                member.OrganizationId,
                member.SubjectId,
                OrganizationMembershipStatus.Active,
                "system:membership-sync"),
            CancellationToken.None);

        Assert.True(suspended.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.Changed, suspended.Value.Outcome);
        Assert.True(replay.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState, replay.Value.Outcome);
        Assert.True(removed.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.Changed, removed.Value.Outcome);
        Assert.True(restored.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.Changed, restored.Value.Outcome);
        Assert.Equal(DomainMembershipState.Active, member.Status);
        Assert.Equal(DomainMembershipRole.Member, member.Role);
        Assert.Equal(4, member.Version);
    }

    [Fact]
    public async Task Owner_membership_is_never_changed_by_the_product_lifecycle_facade()
    {
        TestOrganizationRepository repository = CreateRepository();
        OrganizationMembership owner = repository.Memberships.Single(item => item.Role == DomainMembershipRole.Owner);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationMembershipStateCommand,
            OrganizationMembershipLifecycleResult>>();

        Result<OrganizationMembershipLifecycleResult> result = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                owner.OrganizationId,
                owner.SubjectId,
                OrganizationMembershipStatus.Suspended,
                "system:membership-sync"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.OwnerProtected, result.Value.Outcome);
        Assert.Equal(DomainMembershipState.Active, owner.Status);
        Assert.Equal(1, owner.Version);
    }

    [Fact]
    public async Task Removed_member_cannot_be_suspended_without_an_explicit_restore()
    {
        TestOrganizationRepository repository = CreateRepository();
        OrganizationMembership member = repository.Memberships.Single(item => item.Role == DomainMembershipRole.Member);
        Assert.True(member.Remove(member.Version, "owner", Guid.NewGuid(), Now).IsSuccess);
        using ServiceProvider services = CreateServices(repository);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationMembershipStateCommand,
            OrganizationMembershipLifecycleResult>>();

        Result<OrganizationMembershipLifecycleResult> result = await handler.HandleAsync(
            new EnsureOrganizationMembershipStateCommand(
                member.OrganizationId,
                member.SubjectId,
                OrganizationMembershipStatus.Suspended,
                "system:membership-sync"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationMembershipLifecycleOutcome.TransitionNotAllowed, result.Value.Outcome);
        Assert.Equal(DomainMembershipState.Removed, member.Status);
        Assert.Equal(2, member.Version);
    }

    [Fact]
    public async Task Membership_restore_uses_mutation_admission_before_state_change()
    {
        TestOrganizationRepository repository = CreateRepository();
        OrganizationMembership member = repository.Memberships.Single(
            item => item.Role == DomainMembershipRole.Member);
        Assert.True(member.Remove(
            member.Version,
            "owner",
            Guid.NewGuid(),
            Now).IsSuccess);
        RecordingMutationPolicy policy = new(
            OrganizationMutationAdmissionDecision.Denied);
        using ServiceProvider services = CreateServices(repository, policy);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationMembershipStateCommand,
            OrganizationMembershipLifecycleResult>>();

        Result<OrganizationMembershipLifecycleResult> result =
            await handler.HandleAsync(
                new EnsureOrganizationMembershipStateCommand(
                    member.OrganizationId,
                    member.SubjectId,
                    OrganizationMembershipStatus.Active,
                    "system:membership-sync"),
                CancellationToken.None);

        Assert.Equal(
            OrganizationApplicationErrors.MutationRejected,
            result.Error);
        Assert.Equal(DomainMembershipState.Removed, member.Status);
        Assert.Equal(2, member.Version);
        OrganizationMutationAdmissionContext context =
            Assert.Single(policy.Contexts);
        Assert.Equal(
            OrganizationMutationAdmissionOperation.RestoreMembership,
            context.Operation);
        Assert.Equal(member.Id, context.TargetId);
        Assert.Equal(member.SubjectId, context.TargetSubjectId);
    }

    [Fact]
    public async Task Idempotent_active_replay_and_access_reduction_bypass_mutation_admission()
    {
        TestOrganizationRepository repository = CreateRepository();
        OrganizationMembership member = repository.Memberships.Single(
            item => item.Role == DomainMembershipRole.Member);
        RecordingMutationPolicy policy = new(
            OrganizationMutationAdmissionDecision.Denied);
        using ServiceProvider services = CreateServices(repository, policy);
        var handler = services.GetRequiredService<ICommandHandler<
            EnsureOrganizationMembershipStateCommand,
            OrganizationMembershipLifecycleResult>>();

        Result<OrganizationMembershipLifecycleResult> replay =
            await handler.HandleAsync(
                new EnsureOrganizationMembershipStateCommand(
                    member.OrganizationId,
                    member.SubjectId,
                    OrganizationMembershipStatus.Active,
                    "system:membership-sync"),
                CancellationToken.None);
        Result<OrganizationMembershipLifecycleResult> suspended =
            await handler.HandleAsync(
                new EnsureOrganizationMembershipStateCommand(
                    member.OrganizationId,
                    member.SubjectId,
                    OrganizationMembershipStatus.Suspended,
                    "system:membership-sync"),
                CancellationToken.None);
        Result<OrganizationMembershipLifecycleResult> removed =
            await handler.HandleAsync(
                new EnsureOrganizationMembershipStateCommand(
                    member.OrganizationId,
                    member.SubjectId,
                    OrganizationMembershipStatus.Removed,
                    "system:membership-sync"),
                CancellationToken.None);

        Assert.Equal(
            OrganizationMembershipLifecycleOutcome.AlreadyInDesiredState,
            replay.Value.Outcome);
        Assert.Equal(
            OrganizationMembershipLifecycleOutcome.Changed,
            suspended.Value.Outcome);
        Assert.Equal(
            OrganizationMembershipLifecycleOutcome.Changed,
            removed.Value.Outcome);
        Assert.Empty(policy.Contexts);
    }

    private static TestOrganizationRepository CreateRepository()
    {
        Organization organization = Organization.Create(
            Guid.NewGuid(), "Harbor House", "harbor-house", "owner", Guid.NewGuid(), Now).Value;
        OrganizationMembership owner = OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "owner", DomainMembershipRole.Owner,
            "owner", Guid.NewGuid(), Now).Value;
        TestOrganizationRepository repository = new(organization, owner);
        repository.Memberships.Add(OrganizationMembership.Create(
            Guid.NewGuid(), organization.Id, "member", DomainMembershipRole.Member,
            "owner", Guid.NewGuid(), Now).Value);
        return repository;
    }

    private static ServiceProvider CreateServices(
        TestOrganizationRepository repository,
        IOrganizationMutationAdmissionPolicy? mutationPolicy = null)
    {
        ServiceCollection services = new();
        services.AddOrganizationsApplication(new ConfigurationBuilder().Build());
        if (mutationPolicy is not null)
        {
            services.AddSingleton(mutationPolicy);
        }

        services.AddSingleton<IOrganizationRepository>(repository);
        services.AddSingleton<ISystemClock>(new TestClock(Now.AddMinutes(1)));
        services.AddSingleton<IIdGenerator>(new TestIds());
        return services.BuildServiceProvider();
    }

    private sealed class RecordingMutationPolicy(
        OrganizationMutationAdmissionDecision decision)
        : IOrganizationMutationAdmissionPolicy
    {
        public List<OrganizationMutationAdmissionContext> Contexts { get; } =
            [];

        public ValueTask<OrganizationMutationAdmissionDecision> EvaluateAsync(
            OrganizationMutationAdmissionContext context,
            CancellationToken cancellationToken = default)
        {
            this.Contexts.Add(context);
            return ValueTask.FromResult(decision);
        }
    }
}
