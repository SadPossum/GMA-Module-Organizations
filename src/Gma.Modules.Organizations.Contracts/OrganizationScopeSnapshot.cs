namespace Gma.Modules.Organizations.Contracts;

public sealed record OrganizationScopeSnapshot(
    OrganizationScopeStatus Status,
    long Revision);
