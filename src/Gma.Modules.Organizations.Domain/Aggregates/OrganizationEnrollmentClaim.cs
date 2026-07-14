namespace Gma.Modules.Organizations.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.Errors;
using Gma.Modules.Organizations.Domain.Events;
using Gma.Modules.Organizations.Domain.ValueObjects;

public sealed class OrganizationEnrollmentClaim : AggregateRoot<Guid>
{
    private OrganizationEnrollmentClaim() { }
    private OrganizationEnrollmentClaim(Guid id) : base(id) { }

    public Guid OrganizationId { get; private set; }
    public Guid EnrollmentLinkId { get; private set; }
    public string SubjectId { get; private set; } = string.Empty;
    public OrganizationEnrollmentClaimState Status { get; private set; }
    public Guid? MembershipId { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public static Result<OrganizationEnrollmentClaim> Create(
        Guid id,
        Guid organizationId,
        Guid enrollmentLinkId,
        string subjectId,
        OrganizationEnrollmentClaimState status,
        Guid? membershipId,
        string actorId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(subjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(actorId);
        bool valid = id != Guid.Empty && organizationId != Guid.Empty && enrollmentLinkId != Guid.Empty &&
            status is OrganizationEnrollmentClaimState.Pending or OrganizationEnrollmentClaimState.Accepted &&
            (status == OrganizationEnrollmentClaimState.Pending ? membershipId is null : membershipId is not null);
        if (subject.IsFailure || actor.IsFailure || !valid || eventId == Guid.Empty)
        {
            Error error = subject.IsFailure ? subject.Error : actor.IsFailure ? actor.Error :
                eventId == Guid.Empty ? OrganizationDomainErrors.EventIdRequired :
                OrganizationDomainErrors.EnrollmentConfigurationInvalid;
            return Result.Failure<OrganizationEnrollmentClaim>(error);
        }

        OrganizationEnrollmentClaim claim = new(id)
        {
            OrganizationId = organizationId,
            EnrollmentLinkId = enrollmentLinkId,
            SubjectId = subject.Value.Value,
            Status = status,
            MembershipId = membershipId,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor.Value.Value,
            LastChangedAtUtc = nowUtc
        };
        claim.RaiseChange(eventId, nowUtc, status == OrganizationEnrollmentClaimState.Pending
            ? OrganizationEnrollmentClaimChangeKind.Requested
            : OrganizationEnrollmentClaimChangeKind.Accepted);
        return Result.Success(claim);
    }

    public Result Approve(Guid membershipId, long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result pending = this.EnsurePending(expectedVersion, actorId, eventId);
        if (pending.IsFailure || membershipId == Guid.Empty)
        {
            return pending.IsFailure ? pending : Result.Failure(OrganizationDomainErrors.MembershipIdRequired);
        }

        this.Status = OrganizationEnrollmentClaimState.Accepted;
        this.MembershipId = membershipId;
        this.Advance(actorId.Trim(), nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationEnrollmentClaimChangeKind.Accepted);
        return Result.Success();
    }

    public Result Reject(long expectedVersion, string actorId, Guid eventId, DateTimeOffset nowUtc)
    {
        Result pending = this.EnsurePending(expectedVersion, actorId, eventId);
        if (pending.IsFailure)
        {
            return pending;
        }

        this.Status = OrganizationEnrollmentClaimState.Rejected;
        this.Advance(actorId.Trim(), nowUtc);
        this.RaiseChange(eventId, nowUtc, OrganizationEnrollmentClaimChangeKind.Rejected);
        return Result.Success();
    }

    private Result EnsurePending(long expectedVersion, string actorId, Guid eventId)
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

        return this.Status == OrganizationEnrollmentClaimState.Pending
            ? Result.Success()
            : Result.Failure(OrganizationDomainErrors.EnrollmentClaimUnavailable);
    }

    private void Advance(string actorId, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actorId;
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseChange(Guid eventId, DateTimeOffset nowUtc, OrganizationEnrollmentClaimChangeKind change) =>
        this.RaiseDomainEvent(new OrganizationEnrollmentClaimChangedDomainEvent(
            eventId, nowUtc, this.OrganizationId, this.EnrollmentLinkId,
            this.Id, this.SubjectId, change, this.Status, this.MembershipId, this.Version));
}
