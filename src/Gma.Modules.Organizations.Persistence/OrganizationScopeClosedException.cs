namespace Gma.Modules.Organizations.Persistence;

internal sealed class OrganizationScopeClosedException()
    : InvalidOperationException("The organization scope is closed.");
