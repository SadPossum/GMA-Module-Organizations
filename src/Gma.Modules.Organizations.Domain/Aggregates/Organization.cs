namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed class Organization : AggregateRoot<Guid>
{
    private Organization() { }
    private Organization(Guid id) : base(id) { }

    public string ScopeId => this.Id.ToString("D");
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public OrganizationState Status { get; private set; } = OrganizationState.Active;
    public int ActiveOwnerCount { get; private set; }
    public long Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<Organization> Create(
        Guid id,
        string name,
        string slug,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Organization>(OrganizationDomainErrors.OrganizationIdRequired);
        }

        Result<OrganizationName> organizationName = OrganizationName.Create(name);
        if (organizationName.IsFailure)
        {
            return Result.Failure<Organization>(organizationName.Error);
        }

        Result<OrganizationSlug> organizationSlug = OrganizationSlug.Create(slug);
        if (organizationSlug.IsFailure)
        {
            return Result.Failure<Organization>(organizationSlug.Error);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (actor.IsFailure)
        {
            return Result.Failure<Organization>(actor.Error);
        }

        Result eventValidation = ValidateEvent(eventId);
        if (eventValidation.IsFailure)
        {
            return Result.Failure<Organization>(eventValidation.Error);
        }

        Organization organization = new(id)
        {
            Name = organizationName.Value.Value,
            Slug = organizationSlug.Value.Value,
            Status = OrganizationState.Active,
            ActiveOwnerCount = 1,
            CreatedBy = actor.Value.Value,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor.Value.Value,
            LastChangedAtUtc = nowUtc
        };
        organization.RaiseChange(eventId, nowUtc, OrganizationChangeKind.Created);
        return Result.Success(organization);
    }

    public Result UpdateProfile(
        string name,
        string slug,
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        Result<OrganizationName> organizationName = OrganizationName.Create(name);
        if (organizationName.IsFailure)
        {
            return Result.Failure(organizationName.Error);
        }

        Result<OrganizationSlug> organizationSlug = OrganizationSlug.Create(slug);
        if (organizationSlug.IsFailure)
        {
            return Result.Failure(organizationSlug.Error);
        }

        this.Name = organizationName.Value.Value;
        this.Slug = organizationSlug.Value.Value;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.ProfileUpdated);
        return Result.Success();
    }

    public Result Suspend(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status == OrganizationState.Suspended)
        {
            return Result.Failure(OrganizationDomainErrors.OrganizationAlreadySuspended);
        }

        this.Status = OrganizationState.Suspended;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.Suspended);
        return Result.Success();
    }

    public Result Reactivate(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status != OrganizationState.Suspended)
        {
            return Result.Failure(OrganizationDomainErrors.OrganizationNotSuspended);
        }

        this.Status = OrganizationState.Active;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.Reactivated);
        return Result.Success();
    }

    public Result Archive(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status != OrganizationState.Suspended)
        {
            return Result.Failure(OrganizationDomainErrors.OrganizationNotSuspended);
        }

        this.Status = OrganizationState.Archived;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.Archived);
        return Result.Success();
    }

    public Result AddActiveOwner(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion, actorId, eventId);
        if (active.IsFailure)
        {
            return active;
        }

        this.ActiveOwnerCount++;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.OwnerCountChanged);
        return Result.Success();
    }

    public Result RemoveActiveOwner(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion, actorId, eventId);
        if (active.IsFailure)
        {
            return active;
        }

        if (this.ActiveOwnerCount <= 1)
        {
            return Result.Failure(OrganizationDomainErrors.LastActiveOwner);
        }

        this.ActiveOwnerCount--;
        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.OwnerCountChanged);
        return Result.Success();
    }

    public Result RecordOwnerTransfer(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result active = this.EnsureActive(expectedVersion, actorId, eventId);
        if (active.IsFailure)
        {
            return active;
        }

        this.Advance(actorId, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationChangeKind.OwnershipTransferred);
        return Result.Success();
    }

    private Result EnsureMutable(long expectedVersion, string actorId, Guid eventId)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        if (this.Status == OrganizationState.Archived)
        {
            return Result.Failure(OrganizationDomainErrors.OrganizationArchived);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        return actor.IsFailure ? Result.Failure(actor.Error) : ValidateEvent(eventId);
    }

    private Result EnsureActive(long expectedVersion, string actorId, Guid eventId)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        return this.Status == OrganizationState.Active
            ? Result.Success()
            : Result.Failure(OrganizationDomainErrors.OrganizationNotActive);
    }

    private void Advance(string actorId, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actorId.Trim();
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseChange(Guid eventId, DateTimeOffset nowUtc, OrganizationChangeKind changeKind) =>
        this.RaiseDomainEvent(new OrganizationChangedDomainEvent(
            eventId, nowUtc, this.Id, changeKind, this.Status, this.Version));

    private static Result ValidateEvent(Guid eventId) => eventId == Guid.Empty
        ? Result.Failure(OrganizationDomainErrors.EventIdRequired)
        : Result.Success();

}
