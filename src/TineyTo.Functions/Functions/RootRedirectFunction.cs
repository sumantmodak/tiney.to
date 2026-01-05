using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TineyTo.Functions.Functions;

public class RootRedirectFunction
{
    private readonly ILogger<RootRedirectFunction> _logger;

    public RootRedirectFunction(ILogger<RootRedirectFunction> logger)
    {
        _logger = logger;
    }

    [Function("RootRedirect")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "")] HttpRequest req)
    {
        _logger.LogInformation("Root path accessed, redirecting to www.tiney.to");
        return new RedirectResult("https://www.tiney.to", permanent: true);
    }
}
