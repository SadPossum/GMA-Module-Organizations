namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;
using Gma.Modules.Organizations.Contracts;

internal sealed record ProvisionOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string Slug,
    string InitialOwnerSubjectId,
    string ActorId) : ITransactionalCommand<OrganizationProvisioningResult>;
