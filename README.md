# tiney.to - URL Shortener

A serverless URL shortener built with Azure Functions (.NET 8 Isolated Worker) and Azure Table Storage.

## Features

- **Shorten URLs** - Generate short aliases for long URLs
- **TTL Support** - Set expiration time for links (default: 30 days)
- **Automatic Cleanup** - Timer-triggered function removes expired links

## Project Structure

```
src/
├── TineyTo.Functions/          # Azure Functions app
│   ├── Functions/              # HTTP and Timer triggers
│   ├── Models/                 # Request/Response DTOs
│   ├── Services/               # Business logic
│   └── Storage/                # Table Storage repositories
└── TineyTo.Functions.Tests/    # Unit tests (xUnit + Moq)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://docs.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://docs.microsoft.com/azure/storage/common/storage-use-azurite) (local storage emulator)

## Getting Started

### 1. Start Azurite

```powershell
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

### 2. Run the Functions App

```powershell
cd src/TineyTo.Functions
func start
```

The app will be available at `http://localhost:7071`

## API Endpoints

### Health Check
```http
GET /api/health
```

### Shorten URL
```http
POST /api/shorten
Content-Type: application/json

{
  "url": "https://example.com/very/long/url",
  "ttlDays": 7                  // optional, default: 30
}
```

**Response:**
```json
{
  "alias": "myalias",
  "shortUrl": "http://localhost:7071/myalias",
  "expiresAt": "2025-12-27T00:00:00Z"
}
```

### Redirect
```http
GET /{alias}
```
Redirects to the original URL (HTTP 302)

## Running Tests

```powershell
cd src/TineyTo.Functions.Tests
dotnet test
```

With coverage:
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

Environment variables in `local.settings.json`:

| Setting | Description | Default |
|---------|-------------|---------|
| `AzureWebJobsStorage` | Storage connection string | `UseDevelopmentStorage=true` |
| `BaseUrl` | Base URL for short links | `http://localhost:7071` |
| `DefaultTtlDays` | Default link expiration | `30` |
| `MaxTtlDays` | Maximum allowed TTL | `365` |

## Architecture

- **Two-Table TTL Pattern**: Primary `ShortUrls` table partitioned by alias prefix, `ShortUrlsExpiryIndex` table partitioned by expiry date for efficient cleanup
- **Base62 Aliases**: 6-character aliases using `[0-9A-Za-z]` (56+ billion combinations)
- **Blob Lease Locking**: Prevents concurrent GC execution

## License

MIT
