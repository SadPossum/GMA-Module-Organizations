namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ExpireOrganizationEnrollmentClaimsCommand(int BatchSize)
    : ITransactionalCommand<int>;
