using TineyTo.Functions.Configuration;
using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Services;

public class AliasGeneratorTests
{
    [Fact]
    public void Generate_ReturnsStringOfCorrectLength()
    {
        // Arrange
        var config = new ApplicationConfiguration { AliasLength = 6 };
        var generator = new AliasGenerator(config);

        // Act
        var alias = generator.Generate();

        // Assert
        Assert.Equal(6, alias.Length);
    }

    [Fact]
    public void Generate_ReturnsOnlyBase62Characters()
    {
        // Arrange
        var config = new ApplicationConfiguration { AliasLength = 6 };
        var generator = new AliasGenerator(config);
        const string base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        // Act
        var alias = generator.Generate();

        // Assert
        Assert.All(alias, c => Assert.Contains(c, base62Chars));
    }

    [Fact]
    public void Generate_ReturnsDifferentValuesOnMultipleCalls()
    {
        // Arrange
        var config = new ApplicationConfiguration { AliasLength = 6 };
        var generator = new AliasGenerator(config);

        // Act
        var aliases = Enumerable.Range(0, 100).Select(_ => generator.Generate()).ToList();

        // Assert - should have mostly unique values (allowing for rare collisions)
        var uniqueCount = aliases.Distinct().Count();
        Assert.True(uniqueCount > 90, $"Expected mostly unique aliases, got {uniqueCount} unique out of 100");
    }
}
