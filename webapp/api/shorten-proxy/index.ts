import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";

export async function shortenProxy(
  request: HttpRequest,
  context: InvocationContext
): Promise<HttpResponseInit> {
  context.log("Shorten proxy function processing request");

  // Get configuration from environment variables
  const backendUrl = process.env.BACKEND_URL || "http://localhost:7071";
  const apiKey = process.env.BACKEND_API_KEY;

  if (!apiKey) {
    context.error("BACKEND_API_KEY not configured");
    return {
      status: 500,
      jsonBody: { error: "Proxy configuration error" },
    };
  }

  // Extract real client IP from Azure Static Web Apps headers
  // SWA provides the client IP in X-Forwarded-For header
  const forwardedFor = request.headers.get("x-forwarded-for");
  const clientIp = forwardedFor ? forwardedFor.split(",")[0].trim() : "unknown";

  context.log(`Client IP extracted: ${clientIp}`);

  try {
    // Get request body
    const body = await request.text();

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

    return {
      status: response.status,
      headers: {
        "Content-Type": "application/json",
      },
      jsonBody: data,
    };
  } catch (error) {
    context.error("Error forwarding request to backend:", error);
    return {
      status: 502,
      jsonBody: {
        error: "Failed to connect to backend service",
        message:
          error instanceof Error ? error.message : "Unknown error occurred",
      },
    };
  }
}

app.http("shorten-proxy", {
  methods: ["POST"],
  authLevel: "anonymous",
  route: "shorten-proxy",
  handler: shortenProxy,
});
