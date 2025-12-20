using Microsoft.Extensions.Logging;
using Moq;
using TineyTo.Functions.Functions;
using Microsoft.AspNetCore.Mvc;

namespace TineyTo.Functions.Tests.Functions;

public class HealthFunctionTests
{
    [Fact]
    public void Run_ReturnsOkWithStatus()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<HealthFunction>>();
        var function = new HealthFunction(loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = function.Run(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        var value = okResult.Value;
        var statusProperty = value.GetType().GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("ok", statusProperty.GetValue(value));
    }
}
