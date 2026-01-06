# tiney.to - URL Shortener

A serverless URL shortener built with Azure Functions (.NET 8) and Azure Table Storage.

## Features

- **URL Shortening** - Generate 6-character Base62 aliases
- **Deduplication** - Same URL always returns the same short link
- **Configurable TTL** - Links expire after 60 seconds to 1 year
- **In-Memory Caching** - Fast redirects with LRU cache
- **Automatic Cleanup** - Timer-triggered garbage collection
- **Health Monitoring** - Health check endpoint

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

# Shorten a URL
curl -X POST http://localhost:7071/api/shorten `
  -H "Content-Type: application/json" `
  -d '{"longUrl": "https://example.com/test"}'

# Redirect (replace {alias} with the returned alias)
curl -I http://localhost:7071/{alias}
```

## API Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check |
| `/api/shorten` | POST | Create short URL |
| `/{alias}` | GET | Redirect to long URL |

### POST /api/shorten

```json
{
  "longUrl": "https://example.com/path",
  "expiresInSeconds": 604800
}
```

**Responses:** `201 Created` (new) / `200 OK` (existing) / `400 Bad Request`

## Project Structure

```
tiney/
├── infra/                    # Bicep infrastructure
├── src/
│   ├── TineyTo.Functions/    # Azure Functions app
│   └── TineyTo.Functions.Tests/
└── webapp/                   # React frontend
```

## Deployment

See [infra/README.md](infra/README.md) for Azure deployment instructions.

## Configuration

Key environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `BaseUrl` | - | Base URL for short links (required in Azure) |
| `DefaultTtlDays` | `30` | Default link expiration |
| `CACHE_ENABLED` | `true` | Enable in-memory caching |
| `CACHE_DURATION_SECONDS` | `900` | Cache TTL (15 min) |

## Testing

```powershell
cd src/TineyTo.Functions.Tests
dotnet test
```
