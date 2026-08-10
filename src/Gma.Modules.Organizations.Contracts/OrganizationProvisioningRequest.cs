namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationProvisioningRequest(
    Guid OrganizationId,
    string Name,
    string Slug,
    string InitialOwnerSubjectId,
    string ActorId);
