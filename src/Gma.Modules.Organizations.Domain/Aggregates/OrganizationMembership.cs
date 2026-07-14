namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed class OrganizationMembership : AggregateRoot<Guid>
{
    private OrganizationMembership() { }
    private OrganizationMembership(Guid id) : base(id) { }

    public Guid OrganizationId { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public OrganizationMembershipRole Role { get; private set; }
    public OrganizationMembershipState Status { get; private set; }
    public long Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<OrganizationMembership> Create(
        Guid id,
        Guid organizationId,
        string subjectId,
        OrganizationMembershipRole role,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<OrganizationMembership>(OrganizationDomainErrors.MembershipIdRequired);
        }

        if (organizationId == Guid.Empty)
        {
            return Result.Failure<OrganizationMembership>(OrganizationDomainErrors.OrganizationIdRequired);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (subject.IsFailure || actor.IsFailure || eventId == Guid.Empty ||
            role is not (OrganizationMembershipRole.Member or OrganizationMembershipRole.Owner))
        {
            Error error = subject.IsFailure ? subject.Error : actor.IsFailure ? actor.Error :
                eventId == Guid.Empty ? OrganizationDomainErrors.EventIdRequired : OrganizationDomainErrors.MembershipNotActive;
            return Result.Failure<OrganizationMembership>(error);
        }

        OrganizationMembership membership = new(id)
        {
            OrganizationId = organizationId,
            SubjectId = subject.Value.Value,
            Role = role,
            Status = OrganizationMembershipState.Active,
            CreatedBy = actor.Value.Value,
            JoinedAtUtc = nowUtc,
            LastChangedBy = actor.Value.Value,
            LastChangedAtUtc = nowUtc
        };
        membership.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.Joined);
        return Result.Success(membership);
    }

    public Result Suspend(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status == OrganizationMembershipState.Suspended)
        {
            return Result.Failure(OrganizationDomainErrors.MembershipAlreadySuspended);
        }

        this.Status = OrganizationMembershipState.Suspended;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.Suspended);
        return Result.Success();
    }

    public Result Resume(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status != OrganizationMembershipState.Suspended)
        {
            return Result.Failure(OrganizationDomainErrors.MembershipNotSuspended);
        }

        this.Status = OrganizationMembershipState.Active;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.Resumed);
        return Result.Success();
    }

    public Result Remove(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        this.Status = OrganizationMembershipState.Removed;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.Removed);
        return Result.Success();
    }

    public Result PromoteToOwner(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion, actorId, eventId);
        if (active.IsFailure)
        {
            return active;
        }

        if (this.Role == OrganizationMembershipRole.Owner)
        {
            return Result.Failure(OrganizationDomainErrors.MembershipAlreadyOwner);
        }

        this.Role = OrganizationMembershipRole.Owner;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.PromotedToOwner);
        return Result.Success();
    }

    public Result DemoteToMember(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion, actorId, eventId);
        if (active.IsFailure)
        {
            return active;
        }

        if (this.Role != OrganizationMembershipRole.Owner)
        {
            return Result.Failure(OrganizationDomainErrors.MembershipNotOwner);
        }

        this.Role = OrganizationMembershipRole.Member;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.DemotedToMember);
        return Result.Success();
    }

    public Result RestoreAsMember(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        if (this.Status == OrganizationMembershipState.Active)
        {
            return Result.Success();
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (actor.IsFailure || eventId == Guid.Empty)
        {
            return actor.IsFailure ? Result.Failure(actor.Error) :
                Result.Failure(OrganizationDomainErrors.EventIdRequired);
        }

        this.Role = OrganizationMembershipRole.Member;
        this.Status = OrganizationMembershipState.Active;
        this.Advance(actor.Value.Value, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationMembershipChangeKind.Restored);
        return Result.Success();
    }

    private Result EnsureMutable(long expectedVersion, string actorId, Guid eventId)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        if (this.Status == OrganizationMembershipState.Removed)
        {
            return Result.Failure(OrganizationDomainErrors.MembershipRemoved);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure(actor.Error);
        }

        return eventId == Guid.Empty
            ? Result.Failure(OrganizationDomainErrors.EventIdRequired)
            : Result.Success();
    }

    private Result EnsureActive(long expectedVersion, string actorId, Guid eventId)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        return this.Status == OrganizationMembershipState.Active
            ? Result.Success()
            : Result.Failure(OrganizationDomainErrors.MembershipNotActive);
    }

    private void Advance(string actorId, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actorId.Trim();
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseChange(Guid eventId, DateTimeOffset nowUtc, OrganizationMembershipChangeKind changeKind) =>
        this.RaiseDomainEvent(new OrganizationMembershipChangedDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.Id, this.SubjectId,
            changeKind, this.Role, this.Status, this.Version));
}
