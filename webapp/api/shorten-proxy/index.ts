import { Context, HttpRequest } from "@azure/functions";

module.exports = async function (context: Context, req: HttpRequest): Promise<void> {
  context.log("Shorten proxy function processing request");

  // Get configuration from environment variables
  const backendUrl = process.env.BACKEND_URL || "http://localhost:7071";
  const apiKey = process.env.BACKEND_API_KEY;

  if (!apiKey) {
    context.log.error("BACKEND_API_KEY not configured");
    context.res = {
      status: 500,
      body: { error: "Proxy configuration error" },
    };
    return;
  }

  // Extract real client IP from Azure Static Web Apps headers
  // SWA provides the client IP in X-Forwarded-For header
  const forwardedFor = req.headers["x-forwarded-for"] as string;
  const clientIp = forwardedFor ? forwardedFor.split(",")[0].trim() : "unknown";

  context.log(`Client IP extracted: ${clientIp}`);

  try {
    // Get request body
    const body = JSON.stringify(req.body);

    // Forward request to backend with API key
    const backendEndpoint = `${backendUrl}/api/shorten`;
    
    const response = await fetch(backendEndpoint, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-API-Key": apiKey,
        // Forward client IP to backend for rate limiting
        "X-Forwarded-For": clientIp,
        "X-Real-IP": clientIp,
        "X-Original-IP": clientIp,
      },
      body: body,
    });

    const data = await response.json();

    context.log(
      `Backend responded with status ${response.status} for client IP ${clientIp}`
    );

    context.res = {
      status: response.status,
      headers: {
        "Content-Type": "application/json",
      },
      body: data,
    };
  } catch (error) {
    context.log.error("Error forwarding request to backend:", error);
    context.res = {
      status: 502,
      body: {
        error: "Failed to connect to backend service",
        message:
          error instanceof Error ? error.message : "Unknown error occurred",
      },
    };
  }
};
