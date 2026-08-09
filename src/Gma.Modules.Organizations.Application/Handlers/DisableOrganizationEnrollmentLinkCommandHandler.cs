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

internal sealed class DisableOrganizationEnrollmentLinkCommandHandler(
    IOrganizationRepository organizations,
    OrganizationJoinSourceAuthorization joinSourceAuthorization,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<
        DisableOrganizationEnrollmentLinkCommand,
        OrganizationEnrollmentLinkDto>
{
    public async Task<Result<OrganizationEnrollmentLinkDto>> HandleAsync(
        DisableOrganizationEnrollmentLinkCommand command,
        CancellationToken cancellationToken)
    {
        Result authorized = await joinSourceAuthorization.AuthorizeAsync(
            new OrganizationJoinSourceAuthorizationContext(
                OrganizationJoinSourceAuthorizationOperation.DisableEnrollmentLink,
                command.OrganizationId,
                command.SubjectId,
                command.EnrollmentLinkId),
            cancellationToken).ConfigureAwait(false);
        if (authorized.IsFailure)
        {
            return Result.Failure<OrganizationEnrollmentLinkDto>(authorized.Error);
        }

        OrganizationEnrollmentLink? link = await organizations.GetEnrollmentLinkAsync(
            command.OrganizationId,
            command.EnrollmentLinkId,
            cancellationToken).ConfigureAwait(false);
        if (link is null)
        {
            return Result.Failure<OrganizationEnrollmentLinkDto>(
                OrganizationApplicationErrors.EnrollmentLinkNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result disabled = link.Disable(
            command.ExpectedVersion,
            command.ActorId,
            ids.NewId(),
            nowUtc);
        return disabled.IsSuccess
            ? Result.Success(link.ToDto(nowUtc))
            : Result.Failure<OrganizationEnrollmentLinkDto>(disabled.Error);
    }
}
