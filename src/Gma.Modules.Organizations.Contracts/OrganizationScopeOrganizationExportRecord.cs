namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeOrganizationExportRecord(
    Guid OrganizationId,
    string Name,
    string Slug,
    OrganizationStatus Status,
    int ActiveOwnerCount,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    string LastChangedBy,
    DateTimeOffset LastChangedAtUtc)
    : OrganizationScopeExportRecord;
