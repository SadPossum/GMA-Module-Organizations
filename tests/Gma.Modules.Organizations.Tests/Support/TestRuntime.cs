namespace Gma.Modules.Organizations.Tests.Support;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class TestClock(DateTimeOffset nowUtc) : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = nowUtc;
}

internal sealed class TestIds : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
