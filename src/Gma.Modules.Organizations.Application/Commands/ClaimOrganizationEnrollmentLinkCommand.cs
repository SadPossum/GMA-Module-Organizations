namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

public sealed record ClaimOrganizationEnrollmentLinkCommand(
    string Token,
    string SubjectId,
    string ActorId) : ITransactionalCommand<OrganizationEnrollmentOutcomeDto>;
