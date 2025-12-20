using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Mocks;

public class MockAliasGenerator : IAliasGenerator
{
    private readonly Queue<string> _aliases = new();

    public void SetNextAlias(string alias)
    {
        _aliases.Enqueue(alias);
    }

    public void SetNextAliases(params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            _aliases.Enqueue(alias);
        }
    }

    public string Generate()
    {
        if (_aliases.Count > 0)
        {
            return _aliases.Dequeue();
        }
        return "random";
    }
}
