namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationCreationAdmissionRequest(
    Guid OperationId,
    string Name,
    string Slug,
    string SubjectId,
    string ActorId);
