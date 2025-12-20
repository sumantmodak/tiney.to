using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Mocks;

public class MockTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}
