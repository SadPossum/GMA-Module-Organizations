namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationDto(
    Guid OrganizationId,
    string ScopeId,
    string Name,
    string Slug,
    OrganizationStatus Status,
    int ActiveOwnerCount,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastChangedAtUtc);
