namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed partial class OrganizationEnrollmentLink : AggregateRoot<Guid>
{
    public const int TokenDigestLength = 64;
    public const int AbsoluteMaxClaims = 10_000;

    private OrganizationEnrollmentLink() { }
    private OrganizationEnrollmentLink(Guid id) : base(id) { }

    public Guid OrganizationId { get; private set; }
    public string CreatorSubjectId { get; private set; } = string.Empty;
    public string TokenDigest { get; private set; } = string.Empty;
    public int TokenVersion { get; private set; } = 1;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public int MaximumClaims { get; private set; }
    public int ReservedClaims { get; private set; }
    public OrganizationEnrollmentApprovalMode ApprovalMode { get; private set; }
    public OrganizationEnrollmentLinkState Status { get; private set; }
    public Guid? ReplacesEnrollmentLinkId { get; private set; }
    public long? ReplacesEnrollmentLinkVersion { get; private set; }
    public long Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<OrganizationEnrollmentLink> Create(
        Guid id,
        Guid organizationId,
        string creatorSubjectId,
        string tokenDigest,
        DateTimeOffset expiresAtUtc,
        int maximumClaims,
        OrganizationEnrollmentApprovalMode approvalMode,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc,
        Guid? replacesEnrollmentLinkId = null,
        long? replacesEnrollmentLinkVersion = null)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<OrganizationEnrollmentLink>(OrganizationDomainErrors.EnrollmentLinkIdRequired);
        }

        if (!IsValidReplacement(
                id,
                replacesEnrollmentLinkId,
                replacesEnrollmentLinkVersion))
        {
            return Result.Failure<OrganizationEnrollmentLink>(
                OrganizationDomainErrors.EnrollmentLinkReplacementInvalid);
        }

        Result<OrganizationSubjectId> creator = OrganizationSubjectId.Create(creatorSubjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        bool validConfiguration = organizationId != Guid.Empty && IsValidDigest(tokenDigest) &&
            expiresAtUtc > nowUtc && maximumClaims is >= 1 and <= AbsoluteMaxClaims &&
            approvalMode is OrganizationEnrollmentApprovalMode.Automatic or OrganizationEnrollmentApprovalMode.RequiresApproval;
        if (creator.IsFailure || actor.IsFailure || !validConfiguration || eventId == Guid.Empty)
        {
            Error error = creator.IsFailure ? creator.Error : actor.IsFailure ? actor.Error :
                eventId == Guid.Empty ? OrganizationDomainErrors.EventIdRequired :
                OrganizationDomainErrors.EnrollmentConfigurationInvalid;
            return Result.Failure<OrganizationEnrollmentLink>(error);
        }

        OrganizationEnrollmentLink link = new(id)
        {
            OrganizationId = organizationId,
            CreatorSubjectId = creator.Value.Value,
            TokenDigest = tokenDigest.ToLowerInvariant(),
            ExpiresAtUtc = expiresAtUtc,
            MaximumClaims = maximumClaims,
            ApprovalMode = approvalMode,
            Status = OrganizationEnrollmentLinkState.Active,
            ReplacesEnrollmentLinkId = replacesEnrollmentLinkId,
            ReplacesEnrollmentLinkVersion = replacesEnrollmentLinkVersion,
            CreatedBy = actor.Value.Value,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor.Value.Value,
            LastChangedAtUtc = nowUtc
        };
        link.RaiseChange(eventId, nowUtc, OrganizationEnrollmentLinkChangeKind.Created);
        return Result.Success(link);
    }

    public Result EnsureClaimable(DateTimeOffset nowUtc)
    {
        if (this.Status != OrganizationEnrollmentLinkState.Active)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentLinkUnavailable);
        }

        if (this.ExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentLinkExpired);
        }

        return this.ReservedClaims >= this.MaximumClaims
            ? Result.Failure(OrganizationDomainErrors.EnrollmentLinkCapacityReached)
            : Result.Success();
    }

    public Result ReserveClaim(string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result claimable = this.EnsureClaimable(nowUtc);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (claimable.IsFailure || actor.IsFailure || eventId == Guid.Empty)
        {
            return claimable.IsFailure ? claimable : actor.IsFailure ? Result.Failure(actor.Error) :
                Result.Failure(OrganizationDomainErrors.EventIdRequired);
        }

        this.ReservedClaims++;
        this.Advance(actor.Value.Value, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationEnrollmentLinkChangeKind.ClaimReserved);
        return Result.Success();
    }

    public Result ReleaseClaim(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.ReservedClaims <= 0)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentClaimUnavailable);
        }

        this.ReservedClaims--;
        this.Advance(actorId.Trim(), nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationEnrollmentLinkChangeKind.ClaimReleased);
        return Result.Success();
    }

    public Result Disable(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc) =>
        this.End(OrganizationEnrollmentLinkState.Disabled, OrganizationEnrollmentLinkChangeKind.Disabled,
            expectedVersion, actorId, eventId, nowUtc);

    public Result Rotate(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc) =>
        this.End(OrganizationEnrollmentLinkState.Rotated, OrganizationEnrollmentLinkChangeKind.Rotated,
            expectedVersion, actorId, eventId, nowUtc);

    public Result Expire(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result mutable = this.EnsureMutable(expectedVersion, actorId, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        if (this.Status != OrganizationEnrollmentLinkState.Active)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentLinkUnavailable);
        }

        if (this.ExpiresAtUtc > nowUtc)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentConfigurationInvalid);
        }

        this.Status = OrganizationEnrollmentLinkState.Expired;
        this.Advance(actorId.Trim(), nowUtc);
        this.RaiseDomainEvent(new OrganizationEnrollmentLinkExpiredDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.Id, this.ExpiresAtUtc, this.Version));
        return Result.Success();
    }

    private Result End(
        OrganizationEnrollmentLinkState status,
        OrganizationEnrollmentLinkChangeKind change,
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

        if (this.Status != OrganizationEnrollmentLinkState.Active)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentLinkUnavailable);
        }

        if (this.ExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(OrganizationDomainErrors.EnrollmentLinkExpired);
        }

        this.Status = status;
        this.Advance(actorId.Trim(), nowUtc);
        this.RaiseChange(eventId, nowUtc, change);
        return Result.Success();
    }

    private Result EnsureMutable(long expectedVersion, string actorId, Guid eventId)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        return actor.IsFailure ? Result.Failure(actor.Error) : eventId == Guid.Empty
            ? Result.Failure(OrganizationDomainErrors.EventIdRequired)
            : Result.Success();
    }

    private void Advance(string actorId, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actorId;
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseChange(Guid eventId, DateTimeOffset nowUtc, OrganizationEnrollmentLinkChangeKind change) =>
        this.RaiseDomainEvent(new OrganizationEnrollmentLinkChangedDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.Id, change,
            this.Status, this.ReservedClaims, this.Version));

    private static bool IsValidDigest(string? value) => value is { Length: TokenDigestLength } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static bool IsValidReplacement(
        Guid enrollmentLinkId,
        Guid? replacesEnrollmentLinkId,
        long? replacesEnrollmentLinkVersion) =>
        replacesEnrollmentLinkId is null && replacesEnrollmentLinkVersion is null ||
        replacesEnrollmentLinkId is { } predecessorId &&
        predecessorId != Guid.Empty &&
        predecessorId != enrollmentLinkId &&
        replacesEnrollmentLinkVersion is > 0;
}
