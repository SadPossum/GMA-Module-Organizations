namespace Gma.Modules.Organizations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Modules.Organizations.Application.Commands;
using Gma.Modules.Organizations.Application.Mapping;
using Gma.Modules.Organizations.Application.Policies;
using Gma.Modules.Organizations.Application.Ports;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Domain.Enums;
using Gma.Modules.Organizations.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using DomainApprovalMode = Gma.Modules.Organizations.Domain.Enums.OrganizationEnrollmentApprovalMode;

internal sealed class IssueOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    IOrganizationJoinSourceIssuanceCoordinator issuance,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    OrganizationMutationAdmissionPolicy mutationAdmission,
    IOrganizationEnrollmentTokenService tokens,
    IOptions<OrganizationsOptions> options,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        IssueOrganizationEnrollmentLinkCommand,
        OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>
{
    public async Task<Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>> HandleAsync(
        IssueOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command.Request);
        OrganizationEnrollmentLinkIssuanceRequest request = command.Request;
        if (request.SourceId == Guid.Empty)
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIdRequired);
        }

        Result<OrganizationSubjectId> subject = OrganizationSubjectId.Create(request.SubjectId);
        Result<OrganizationActorId> actor = OrganizationActorId.Create(request.ActorId);
        Result<int> lifetime =
            OrganizationEnrollmentHandlerSupport.ResolveLifetimeHours(request.LifetimeHours, options);
        Result<int> claims =
            OrganizationEnrollmentHandlerSupport.ValidateMaximumClaims(request.MaximumClaims, options);
        Result<DomainApprovalMode> mode = OrganizationEnrollmentHandlerSupport.MapMode(request.ApprovalMode);
        if (subject.IsFailure || actor.IsFailure || lifetime.IsFailure || claims.IsFailure || mode.IsFailure)
        {
            return Failure(
                subject.IsFailure ? subject.Error : actor.IsFailure ? actor.Error :
                lifetime.IsFailure ? lifetime.Error : claims.IsFailure ? claims.Error : mode.Error);
        }

        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.IssueEnrollmentLink,
                request.OrganizationId,
                subject.Value.Value,
                request.SourceId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Failure(authorized.Error);
        }

        Organization? organization = await organizations
            .GetOrganizationAsync(request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not { Status: OrganizationState.Active })
        {
            return Failure(
                organization is null
                    ? OrganizationApplicationErrors.OrganizationNotFound
                    : Gma.Modules.Organizations.Domain.Errors.OrganizationDomainErrors.OrganizationNotActive);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        OrganizationEnrollmentLink? existing = await issuance.AcquireEnrollmentLinkAsync(
            request.OrganizationId,
            request.SourceId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            bool exactReplay =
                string.Equals(existing.CreatorSubjectId, subject.Value.Value, StringComparison.Ordinal) &&
                string.Equals(existing.CreatedBy, actor.Value.Value, StringComparison.Ordinal) &&
                existing.ExpiresAtUtc == existing.CreatedAtUtc.AddHours(lifetime.Value) &&
                existing.MaximumClaims == claims.Value &&
                existing.ApprovalMode == mode.Value;
            return exactReplay
                ? Result.Success(new OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>(
                    existing.ToDto(nowUtc),
                    OrganizationJoinSourceIssuanceOutcome.AlreadyIssued,
                    null,
                    null))
                : Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        if (await organizations.EnrollmentLinkIdExistsAsync(request.SourceId, cancellationToken)
                .ConfigureAwait(false) ||
            await organizations.InvitationIdExistsAsync(request.SourceId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Failure(OrganizationApplicationErrors.JoinSourceIssuanceConflict);
        }

        Result admitted = await mutationAdmission.AuthorizeAsync(
            new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.IssueEnrollmentLink,
                request.OrganizationId,
                subject.Value.Value,
                request.SourceId),
            cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Failure(admitted.Error);
        }

        IssuedOrganizationEnrollmentToken issued = tokens.Issue();
        Result<OrganizationEnrollmentLink> link = OrganizationEnrollmentLink.Create(
            request.SourceId,
            organization.Id,
            subject.Value.Value,
            issued.Digest,
            nowUtc.AddHours(lifetime.Value),
            claims.Value,
            mode.Value,
            actor.Value.Value,
            ids.NewId(),
            nowUtc);
        if (link.IsFailure)
        {
            return Failure(link.Error);
        }

        await organizations.AddEnrollmentLinkAsync(link.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(new OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>(
            link.Value.ToDto(nowUtc),
            OrganizationJoinSourceIssuanceOutcome.Issued,
            issued.Secret,
            null));
    }

    private static Result<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>> Failure(
        Error error) => Result.Failure<OrganizationJoinSourceIssuance<OrganizationEnrollmentLinkDto>>(error);
}
