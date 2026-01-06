using Microsoft.AspNetCore.Http;

namespace TineyTo.Functions.Services;

/// <summary>
/// Helper for extracting client IP from HTTP requests.
/// Handles proxy headers and Azure-specific headers.
/// </summary>
public static class ClientIpExtractor
{
    /// <summary>
    /// Extracts the client IP address from an HTTP request.
    /// Checks common proxy headers in order of preference.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The client IP address, or "unknown" if not determinable.</returns>
    public static string GetClientIp(HttpRequest request)
    {
        // Azure Functions specific header (set by Azure infrastructure)
        var azureClientIp = request.Headers["X-Azure-ClientIP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(azureClientIp))
        {
            return NormalizeIp(azureClientIp);
        }

        // Standard forwarded header (RFC 7239)
        var forwarded = request.Headers["Forwarded"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var forPart = forwarded.Split(';')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("for=", StringComparison.OrdinalIgnoreCase));
            
            if (forPart != null)
            {
                var ip = forPart.Substring(4).Trim('"', '[', ']');
                // Handle IPv6 with port
                var colonIndex = ip.LastIndexOf(':');
                if (colonIndex > ip.LastIndexOf(']'))
                {
                    ip = ip.Substring(0, colonIndex);
                }
                return NormalizeIp(ip);
            }
        }

        // X-Forwarded-For (most common proxy header)
        var xForwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xForwardedFor))
        {
            // Take the first (original client) IP
            var firstIp = xForwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return NormalizeIp(firstIp);
            }
        }

        // X-Real-IP (Nginx)
        var xRealIp = request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xRealIp))
        {
            return NormalizeIp(xRealIp);
        }

        // X-Client-IP
        var xClientIp = request.Headers["X-Client-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xClientIp))
        {
            return NormalizeIp(xClientIp);
        }

        // CF-Connecting-IP (Cloudflare)
        var cfConnectingIp = request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cfConnectingIp))
        {
            return NormalizeIp(cfConnectingIp);
        }

        // True-Client-IP (Akamai)
        var trueClientIp = request.Headers["True-Client-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(trueClientIp))
        {
            return NormalizeIp(trueClientIp);
        }

        // Fall back to remote IP address
        var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            // Handle IPv4-mapped IPv6 addresses
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                return remoteIp.MapToIPv4().ToString();
            }
            return remoteIp.ToString();
        }

        return "unknown";
    }

    /// <summary>
    /// Normalizes an IP address string by trimming and handling common formats.
    /// </summary>
    private static string NormalizeIp(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "unknown";

        ip = ip.Trim();

        // Remove port from IPv4 (e.g., "192.168.1.1:12345" -> "192.168.1.1")
        if (ip.Contains('.') && ip.Contains(':'))
        {
            var colonIndex = ip.LastIndexOf(':');
            ip = ip.Substring(0, colonIndex);
        }

        // Handle IPv4-mapped IPv6 format (::ffff:192.168.1.1)
        if (ip.StartsWith("::ffff:", StringComparison.OrdinalIgnoreCase))
        {
            ip = ip.Substring(7);
        }

        return ip;
    }
}
