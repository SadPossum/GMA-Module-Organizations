namespace Gma.Modules.Organizations.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ExpireOrganizationEnrollmentLinksCommand(int BatchSize)
    : ITransactionalCommand<int>;
