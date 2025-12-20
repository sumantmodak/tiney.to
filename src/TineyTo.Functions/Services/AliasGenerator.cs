namespace TineyTo.Functions.Services;

public interface IAliasGenerator
{
    /// <summary>
    /// Generates a random alias of the configured length.
    /// </summary>
    string Generate();
}

public class AliasGenerator : IAliasGenerator
{
    private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private readonly int _aliasLength;
    private readonly Random _random;

    public AliasGenerator()
    {
        var aliasLengthStr = Environment.GetEnvironmentVariable("ALIAS_LENGTH") ?? "6";
        _aliasLength = int.TryParse(aliasLengthStr, out var length) ? length : 6;
        _random = Random.Shared;
    }

    public string Generate()
    {
        var chars = new char[_aliasLength];
        for (int i = 0; i < _aliasLength; i++)
        {
            chars[i] = Base62Chars[_random.Next(Base62Chars.Length)];
        }
        return new string(chars);
    }
}
