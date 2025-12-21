using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Services;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new();

    #region ValidateLongUrl Tests

    [Fact]
    public void ValidateLongUrl_NullUrl_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateLongUrl(null);

        Assert.False(isValid);
        Assert.Equal("longUrl is required", error);
    }

    [Fact]
    public void ValidateLongUrl_EmptyUrl_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("");

        Assert.False(isValid);
        Assert.Equal("longUrl is required", error);
    }

    [Fact]
    public void ValidateLongUrl_WhitespaceUrl_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("   ");

        Assert.False(isValid);
        Assert.Equal("longUrl is required", error);
    }

    [Fact]
    public void ValidateLongUrl_TooLongUrl_ReturnsInvalid()
    {
        var longUrl = "https://example.com/" + new string('a', 4100);

        var (isValid, error) = _validator.ValidateLongUrl(longUrl);

        Assert.False(isValid);
        Assert.Contains("4096", error);
    }

    [Fact]
    public void ValidateLongUrl_RelativeUrl_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("/relative/path");

        Assert.False(isValid);
        Assert.Contains("absolute", error);
    }

    [Fact]
    public void ValidateLongUrl_InvalidScheme_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("ftp://example.com/file");

        Assert.False(isValid);
        Assert.Contains("http or https", error);
    }

    [Fact]
    public void ValidateLongUrl_HttpUrl_ReturnsValid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("http://example.com/path");

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateLongUrl_HttpsUrl_ReturnsValid()
    {
        var (isValid, error) = _validator.ValidateLongUrl("https://example.com/path?query=1");

        Assert.True(isValid);
        Assert.Null(error);
    }

    #endregion

    #region ValidateExpiresInSeconds Tests

    [Fact]
    public void ValidateExpiresInSeconds_Null_ReturnsValid()
    {
        var (isValid, error) = _validator.ValidateExpiresInSeconds(null);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateExpiresInSeconds_TooSmall_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateExpiresInSeconds(30);

        Assert.False(isValid);
        Assert.Contains("60", error);
    }

    [Fact]
    public void ValidateExpiresInSeconds_TooLarge_ReturnsInvalid()
    {
        var (isValid, error) = _validator.ValidateExpiresInSeconds(999999999);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(3600)]
    [InlineData(86400)]
    [InlineData(604800)]
    public void ValidateExpiresInSeconds_ValidValues_ReturnsValid(int seconds)
    {
        var (isValid, error) = _validator.ValidateExpiresInSeconds(seconds);

        Assert.True(isValid);
        Assert.Null(error);
    }

    #endregion
}
