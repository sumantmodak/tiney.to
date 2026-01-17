# Tiney.to - URL Shortener

<img width="743" height="320" alt="image" src="https://github.com/user-attachments/assets/6f5ddefd-c25a-4860-bbb3-70c9959d0bce" />


Visit [Tiney.To](https://tiney.to)

#### A high-performance, production-ready serverless URL shortener built with Azure Functions (.NET 8), Azure Table Storage, and React.
![Azure](https://img.shields.io/badge/azure-%230072C6.svg?style=for-the-badge&logo=microsoftazure&logoColor=white)
![Azure Functions](https://img.shields.io/badge/Azure_Functions-0062AD?style=for-the-badge&logo=azure-functions&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
## Features

### Core Functionality
- **URL Shortening** - Generate 6-character Base62 aliases for any URL
- **Smart Deduplication** - Same URL always returns the same short link
- **Configurable TTL** - Links expire from 60 seconds to 1 year
- **Root Domain Redirect** - Apex domain (tiney.to) redirects to www or processes short links

### Performance & Reliability
- **In-Memory Caching** - Fast redirects with LRU cache (configurable TTL)
- **Rate Limiting** - Sliding window rate limiter to prevent abuse
- **API Key Authentication** - Secure API access with configurable key-based auth
- **Statistics Tracking** - Async event queuing for redirects and shortens

### Operations
- **Automatic Cleanup** - Timer-triggered garbage collection of expired links
- **Health Monitoring** - Health check endpoint for uptime monitoring
- **Statistics Processing** - Background aggregation of usage statistics
- **Comprehensive Logging** - Structured logging with Application Insights integration

### Frontend
- **Modern React UI** - Built with Vite, TypeScript, and Tailwind CSS
- **Responsive Design** - Mobile-friendly interface
- **API Proxy** - Secure backend communication through Static Web Apps managed functions

## Quick Start

### Prerequisites

- .NET SDK 8.0+
- Azure Functions Core Tools 4.x
- Azurite (storage emulator)

### Run Locally

```powershell
# Start storage emulator
azurite --silent --location .azurite

# Start the app
cd src/TineyTo.Functions
func start
```

### Test the API

```powershell
# Health check
curl http://localhost:7071/api/health

# Shorten a URL (requires API key in production)
curl -X POST http://localhost:7071/api/shorten `
  -H "Content-Type: application/json" `
  -H "X-API-Key: your-api-key" `
  -d '{"url": "https://example.com/test", "expiresInSeconds": 604800}'

# Redirect (replace {alias} with the returned alias)
curl -I http://localhost:7071/{alias}

# Test root redirect
curl -I http://localhost:7071/
```

## API Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check endpoint |
| `/api/shorten` | POST | Create short URL (requires API key) |
| `/{alias}` | GET | Redirect to long URL |
| `/` | GET | Root redirect to www or alias-based redirect |

### POST /api/shorten

**Headers:**
- `Content-Type: application/json`
- `X-API-Key: <your-api-key>` (required)

**Request Body:**
```json
{
  "url": "https://example.com/path",
  "expiresInSeconds": 604800
}
```

**Responses:** 
- `201 Created` - New short URL created
- `200 OK` - Existing short URL returned
- `400 Bad Request` - Invalid URL or parameters
- `401 Unauthorized` - Missing or invalid API key
- `429 Too Many Requests` - Rate limit exceeded

## Project Structure

```
tiney/
├── .github/workflows/        # CI/CD workflows
├── infra/                    # Bicep infrastructure as code
│   ├── buildout/            # Azure deployment templates
│   └── Setup-StatisticsQueue.ps1
├── scripts/                  # Utility scripts
│   └── Generate-ApiKey.ps1  # API key generation
├── src/
│   ├── TineyTo.Functions/    # Azure Functions app (.NET 8)
│   │   ├── Configuration/   # App configuration models
│   │   ├── Functions/       # HTTP & timer triggered functions
│   │   ├── Middleware/      # Request processing middleware
│   │   ├── Models/          # Domain models
│   │   ├── Services/        # Business logic & integrations
│   │   └── Storage/         # Azure Table Storage repositories
│   └── TineyTo.Functions.Tests/  # Unit tests
└── webapp/                   # React frontend (Vite + TypeScript)
    ├── api/                 # Static Web Apps managed functions
    │   └── shorten-proxy/   # Backend API proxy
    └── src/                 # React application
```

## Deployment

### Azure Resources

This application deploys to:
- **Azure Functions** (Consumption Plan) - Backend API at apex domain (tiney.to)
- **Azure Static Web Apps** - Frontend at www subdomain (www.tiney.to)
- **Azure Table Storage** - URL mappings and statistics
- **Azure Storage Queue** - Async statistics event processing

### DNS Configuration

| Record Type | Host | Value | Purpose |
|-------------|------|-------|---------|
| A Records | @ | Function App IPs | Backend API (tiney.to/alias) |
| CNAME | www | SWA hostname | Frontend (www.tiney.to) |

### Deployment Steps

See [infra/README.md](infra/README.md) for detailed Azure deployment instructions using Bicep templates.

### Custom Domain Setup

1. **Function App (apex domain):**
   - Add custom domain `tiney.to`
   - Azure automatically provisions SSL certificate
   
2. **Static Web App (www subdomain):**
   - Add custom domain `www.tiney.to`
   - Azure automatically provisions SSL certificate

## Configuration

### Environment Variables

#### Azure Functions Backend

| Variable | Default | Description |
|----------|---------|-------------|
| `BaseUrl` | - | Base URL for short links (e.g., https://tiney.to) |
| `DefaultTtlDays` | `30` | Default link expiration in days |
| `CACHE_ENABLED` | `true` | Enable in-memory caching |
| `CACHE_DURATION_SECONDS` | `900` | Cache TTL (15 minutes) |
| `CACHE_MAX_SIZE` | `10000` | Maximum cache entries |
| `RATE_LIMIT_WINDOW_SECONDS` | `60` | Rate limit window |
| `RATE_LIMIT_MAX_REQUESTS` | `10` | Max requests per window |
| `API_AUTH_ENABLED` | `true` | Enable API key authentication |
| `API_KEYS` | - | Comma-separated list of valid API keys |
| `STATISTICS_ENABLED` | `true` | Enable statistics tracking |
| `STATISTICS_QUEUE_CONNECTION` | - | Azure Storage connection for queue |
| `STATISTICS_QUEUE_NAME` | `statistics-events` | Queue name for events |
| `RootRedirectUrl` | `https://www.tiney.to` | URL for apex domain redirect |

#### Static Web App (Frontend Proxy)

| Variable | Default | Description |
|----------|---------|-------------|
| `BACKEND_URL` | - | Backend Function App URL |
| `BACKEND_API_KEY` | - | API key for backend authentication |

### Generating API Keys

```powershell
# Run the key generator script
.\scripts\Generate-ApiKey.ps1
```

## Testing

```powershell
# Run unit tests
cd src/TineyTo.Functions.Tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Architecture

### Request Flow

1. **Shorten URL:**
   - User → `www.tiney.to` (React UI)
   - React → `/api/shorten-proxy` (SWA managed function)
   - Proxy → Function App `/api/shorten` (with API key)
   - Function App → Table Storage (persist mapping)
   - Statistics event queued asynchronously

2. **Redirect:**
   - User → `tiney.to/abc123` (Function App)
   - Cache check → Table Storage lookup
   - 302 redirect to target URL
   - Statistics event queued asynchronously

### Background Jobs

- **Statistics Processor** - Processes queued events for analytics
- **Expired Link Reaper** - Removes expired links (runs every 6 hours)

## License

MIT
