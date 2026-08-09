namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed class OrganizationInvitation : AggregateRoot<Guid>
{
    public const int TokenDigestLength = 64;

    private OrganizationInvitation() { }
    private OrganizationInvitation(Guid id) : base(id) { }

    public Guid OrganizationId { get; private set; }
    public string InviterSubjectId { get; private set; } = string.Empty;
    public string? RecipientEmail { get; private set; }
    public string TokenDigest { get; private set; } = string.Empty;
    public int TokenVersion { get; private set; } = 1;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public OrganizationInvitationState Status { get; private set; }
    public string? AcceptedSubjectId { get; private set; }
    public Guid? AcceptedMembershipId { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public Guid? ReplacesInvitationId { get; private set; }
    public long? ReplacesInvitationVersion { get; private set; }
    public long Version { get; private set; } = 1;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<OrganizationInvitation> Create(
        Guid id,
        Guid organizationId,
        string inviterSubjectId,
        string? recipientEmail,
        string tokenDigest,
        DateTimeOffset expiresAtUtc,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc,
        Guid? replacesInvitationId = null,
        long? replacesInvitationVersion = null)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<OrganizationInvitation>(OrganizationDomainErrors.InvitationIdRequired);
        }

        if (organizationId == Guid.Empty)
        {
            return Result.Failure<OrganizationInvitation>(OrganizationDomainErrors.OrganizationIdRequired);
        }

        if (!IsValidReplacement(
                id,
                replacesInvitationId,
                replacesInvitationVersion))
        {
            return Result.Failure<OrganizationInvitation>(
                OrganizationDomainErrors.InvitationReplacementInvalid);
        }

        Result<OrganizationSubjectId> inviter = OrganizationSubjectId.Create(inviterSubjectId);
        Result<OrganizationInvitationRecipient> recipient = OrganizationInvitationRecipient.Create(recipientEmail);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (inviter.IsFailure || recipient.IsFailure || actor.IsFailure ||
            !IsValidDigest(tokenDigest) || expiresAtUtc <= nowUtc || eventId == Guid.Empty)
        {
            Error error = inviter.IsFailure ? inviter.Error : recipient.IsFailure ? recipient.Error :
                actor.IsFailure ? actor.Error : !IsValidDigest(tokenDigest)
                    ? OrganizationDomainErrors.InvitationTokenDigestInvalid
                    : expiresAtUtc <= nowUtc ? OrganizationDomainErrors.InvitationExpiryInvalid
                    : OrganizationDomainErrors.EventIdRequired;
            return Result.Failure<OrganizationInvitation>(error);
        }

        OrganizationInvitation invitation = new(id)
        {
            OrganizationId = organizationId,
            InviterSubjectId = inviter.Value.Value,
            RecipientEmail = recipient.Value.Email,
            TokenDigest = tokenDigest.ToLowerInvariant(),
            ExpiresAtUtc = expiresAtUtc,
            Status = OrganizationInvitationState.Pending,
            ReplacesInvitationId = replacesInvitationId,
            ReplacesInvitationVersion = replacesInvitationVersion,
            CreatedBy = actor.Value.Value,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor.Value.Value,
            LastChangedAtUtc = nowUtc
        };
        invitation.RaiseChange(eventId, nowUtc, OrganizationInvitationChangeKind.Created);
        return Result.Success(invitation);
    }

    public Result Accept(
        string subjectId,
        Guid membershipId,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result acceptable = this.EnsureAcceptable(subjectId, nowUtc);
        if (acceptable.IsFailure)
        {
            return acceptable;
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (subject.IsFailure || actor.IsFailure || membershipId == Guid.Empty || eventId == Guid.Empty)
        {
            Error error = subject.IsFailure ? subject.Error : actor.IsFailure ? actor.Error :
                membershipId == Guid.Empty ? OrganizationDomainErrors.MembershipIdRequired :
                OrganizationDomainErrors.EventIdRequired;
            return Result.Failure(error);
        }

        if (this.Status == OrganizationInvitationState.Accepted)
        {
            return this.AcceptedMembershipId == membershipId
                ? Result.Success()
                : Result.Failure(OrganizationDomainErrors.InvitationUnavailable);
        }

        this.Status = OrganizationInvitationState.Accepted;
        this.AcceptedSubjectId = subject.Value.Value;
        this.AcceptedMembershipId = membershipId;
        this.AcceptedAtUtc = nowUtc;
        this.Advance(actor.Value.Value, nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationInvitationChangeKind.Accepted);
        return Result.Success();
    }

    public Result EnsureAcceptable(string subjectId, DateTimeOffset nowUtc)
    {
        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        if (subject.IsFailure)
        {
            return Result.Failure(subject.Error);
        }

        if (this.Status == OrganizationInvitationState.Accepted)
        {
            return string.Equals(this.AcceptedSubjectId, subject.Value.Value, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure(OrganizationDomainErrors.InvitationClaimedByAnotherSubject);
        }

        if (this.Status != OrganizationInvitationState.Pending)
        {
            return Result.Failure(OrganizationDomainErrors.InvitationUnavailable);
        }

        return this.ExpiresAtUtc <= nowUtc
            ? Result.Failure(OrganizationDomainErrors.InvitationExpired)
            : Result.Success();
    }

    public Result Revoke(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc) =>
        this.EndPending(OrganizationInvitationState.Revoked, OrganizationInvitationChangeKind.Revoked,
            expectedVersion, actorId, eventId, nowUtc);

    public Result Supersede(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc) =>
        this.EndPending(OrganizationInvitationState.Superseded, OrganizationInvitationChangeKind.Superseded,
            expectedVersion, actorId, eventId, nowUtc);

    public Result Expire(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (actor.IsFailure || eventId == Guid.Empty)
        {
            return actor.IsFailure ? Result.Failure(actor.Error) :
                Result.Failure(OrganizationDomainErrors.EventIdRequired);
        }

        if (this.Status != OrganizationInvitationState.Pending)
        {
            return Result.Failure(OrganizationDomainErrors.InvitationUnavailable);
        }

        if (this.ExpiresAtUtc > nowUtc)
        {
            return Result.Failure(OrganizationDomainErrors.InvitationExpiryInvalid);
        }

        this.Status = OrganizationInvitationState.Expired;
        this.Advance(actor.Value.Value, nowUtc);
        this.RaiseDomainEvent(new OrganizationInvitationExpiredDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.Id, this.ExpiresAtUtc, this.Version));
        return Result.Success();
    }

    private Result EndPending(
        OrganizationInvitationState state,
        OrganizationInvitationChangeKind change,
        long expectedVersion,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(OrganizationDomainErrors.VersionConflict);
        }

        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        if (actor.IsFailure || eventId == Guid.Empty)
        {
            return actor.IsFailure ? Result.Failure(actor.Error) :
                Result.Failure(OrganizationDomainErrors.EventIdRequired);
        }

        if (this.Status != OrganizationInvitationState.Pending || this.ExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(this.ExpiresAtUtc <= nowUtc
                ? OrganizationDomainErrors.InvitationExpired
                : OrganizationDomainErrors.InvitationUnavailable);
        }

        this.Status = state;
        this.Advance(actor.Value.Value, nowUtc);
        this.RaiseChange(eventId, nowUtc, change);
        return Result.Success();
    }

    private void Advance(string actorId, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actorId;
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseChange(Guid eventId, DateTimeOffset nowUtc, OrganizationInvitationChangeKind changeKind) =>
        this.RaiseDomainEvent(new OrganizationInvitationChangedDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.Id, changeKind, this.Status,
            this.AcceptedSubjectId, this.Version));

    private static bool IsValidDigest(string? value) => value is { Length: TokenDigestLength } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static bool IsValidReplacement(
        Guid invitationId,
        Guid? replacesInvitationId,
        long? replacesInvitationVersion) =>
        replacesInvitationId is null && replacesInvitationVersion is null ||
        replacesInvitationId is { } predecessorId &&
        predecessorId != Guid.Empty &&
        predecessorId != invitationId &&
        replacesInvitationVersion is > 0;
}
