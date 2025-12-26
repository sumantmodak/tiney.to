# tiney.to - URL Shortener

A serverless URL shortener built with Azure Functions (.NET 8 Isolated Worker) and Azure Table Storage.

---

## Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Architecture](#architecture)
4. [System Design](#system-design)
5. [Data Model](#data-model)
6. [API Reference](#api-reference)
7. [Caching Strategy](#caching-strategy)
8. [Expiry & Garbage Collection](#expiry--garbage-collection)
9. [Scalability & Performance](#scalability--performance)
10. [Security Considerations](#security-considerations)
11. [Infrastructure](#infrastructure)
12. [Project Structure](#project-structure)
13. [Getting Started](#getting-started)
14. [Configuration Reference](#configuration-reference)
15. [Testing](#testing)
16. [Deployment](#deployment)

---

## Overview

**tiney.to** is a high-performance, serverless URL shortening service designed for simplicity, cost-efficiency, and horizontal scalability. It leverages Azure's F1 Free Tier for development and can scale to Consumption plan for production workloads.

### Design Goals

| Goal | Description |
|------|-------------|
| **Serverless-First** | Zero infrastructure management, automatic scaling |
| **Cost-Efficient** | F1 Free Tier for dev, pay-per-use Consumption for prod |
| **Low Latency** | Sub-100ms redirect latency with in-memory caching |
| **Highly Available** | Leverages Azure's 99.9% SLA for Functions and Storage |
| **Operationally Simple** | Minimal moving parts, self-healing expiry cleanup |

---

## Features

| Feature | Description |
|---------|-------------|
| **URL Shortening** | Generate random 6-character Base62 aliases for long URLs |
| **URL Deduplication** | Same URL always returns the same short link (via hash-based lookup) |
| **Configurable TTL** | Set expiration time (60 seconds to 90 days, configurable) |
| **In-Memory Caching** | Per-instance LRU cache with configurable size and duration |
| **Negative Caching** | Cache "not found" results to prevent repeated storage lookups |
| **Automatic Cleanup** | Timer-triggered garbage collection removes expired links |
| **Distributed Locking** | Blob lease-based locking prevents concurrent GC execution |
| **Health Monitoring** | Health check endpoint for load balancer probes |

---

## Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Internet                                   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         Azure Front Door (optional)                     │
│                    Custom Domain: https://tiney.to                      │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         Azure Functions (F1 Free Tier)                   │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐ │
│  │ ShortenFunction │  │ RedirectFunction│  │ ExpiredLinkReaperFunction│ │
│  │   POST /api/    │  │   GET /{alias}  │  │   Timer (every 15 min)   │ │
│  │     shorten     │  │                 │  │                         │ │
│  └────────┬────────┘  └────────┬────────┘  └────────────┬────────────┘ │
│           │                    │                        │              │
│           │    ┌───────────────┴───────────────┐        │              │
│           │    │      In-Memory Cache          │        │              │
│           │    │   (per-instance, LRU, 50MB)   │        │              │
│           │    └───────────────┬───────────────┘        │              │
│           │                    │                        │              │
└───────────┼────────────────────┼────────────────────────┼──────────────┘
            │                    │                        │
            ▼                    ▼                        ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        Azure Table Storage                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────┐ │
│  │   ShortUrls     │  │    UrlIndex     │  │  ShortUrlsExpiryIndex   │ │
│  │  (Primary Data) │  │ (Deduplication) │  │    (GC Scan Index)      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        Azure Blob Storage                               │
│  ┌─────────────────────────────────────────────────────────────────────┐│
│  │  locks/expiry-reaper.lock  (Distributed Lock for GC)                ││
│  └─────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      Application Insights                               │
│         (Logging, Metrics, Distributed Tracing)                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **ShortenFunction** | Creates short URLs, handles deduplication, inserts into all tables |
| **RedirectFunction** | Resolves aliases to long URLs, serves 302 redirects |
| **ExpiredLinkReaperFunction** | Cleans up expired entries from all tables |
| **CachingShortUrlRepository** | Decorator providing in-memory caching for redirects |
| **TableShortUrlRepository** | Direct Azure Table Storage access for short URLs |
| **TableExpiryIndexRepository** | Manages expiry index for efficient GC scans |
| **TableUrlIndexRepository** | Manages URL deduplication index |

---

## System Design

### Request Flows

#### Shorten URL Flow

```
Client                Function              UrlIndex        ShortUrls      ExpiryIndex
  │                      │                     │               │               │
  │  POST /api/shorten   │                     │               │               │
  │─────────────────────>│                     │               │               │
  │                      │                     │               │               │
  │                      │  Check existing URL │               │               │
  │                      │────────────────────>│               │               │
  │                      │                     │               │               │
  │                      │  (if exists & valid)│               │               │
  │                      │<────────────────────│               │               │
  │  200 OK (existing)   │                     │               │               │
  │<─────────────────────│                     │               │               │
  │                      │                     │               │               │
  │                      │  (if not exists)    │               │               │
  │                      │  Generate alias     │               │               │
  │                      │  Insert short URL   │               │               │
  │                      │─────────────────────┼──────────────>│               │
  │                      │                     │               │               │
  │                      │  Insert URL index   │               │               │
  │                      │────────────────────>│               │               │
  │                      │                     │               │               │
  │                      │  Insert expiry idx  │               │               │
  │                      │─────────────────────┼───────────────┼──────────────>│
  │                      │                     │               │               │
  │  201 Created         │                     │               │               │
  │<─────────────────────│                     │               │               │
```

#### Redirect Flow

```
Client              Function              Cache           ShortUrls
  │                    │                    │                 │
  │  GET /{alias}      │                    │                 │
  │───────────────────>│                    │                 │
  │                    │                    │                 │
  │                    │  Lookup in cache   │                 │
  │                    │───────────────────>│                 │
  │                    │                    │                 │
  │                    │  Cache HIT         │                 │
  │                    │<───────────────────│                 │
  │  302 Redirect      │                    │                 │
  │<───────────────────│                    │                 │
  │                    │                    │                 │
  │                    │  Cache MISS        │                 │
  │                    │<───────────────────│                 │
  │                    │                    │                 │
  │                    │  Query Table       │                 │
  │                    │────────────────────┼────────────────>│
  │                    │                    │                 │
  │                    │  Store in cache    │                 │
  │                    │───────────────────>│                 │
  │                    │                    │                 │
  │  302 Redirect      │                    │                 │
  │<───────────────────│                    │                 │
```

### Alias Generation Strategy

The system uses **Base62 encoding** (0-9, A-Z, a-z) to generate short aliases:

| Property | Value |
|----------|-------|
| Character Set | `0-9A-Za-z` (62 characters) |
| Default Length | 6 characters |
| Possible Combinations | 62^6 = **56.8 billion** |
| Collision Handling | Retry with new alias (up to 10 attempts) |

```csharp
// Alias generation formula
Capacity = 62^n where n = alias length

// For n=6: 56,800,235,584 possible aliases
// For n=7: 3,521,614,606,208 possible aliases
```

### URL Deduplication

The system uses a **SHA256 hash-based reverse lookup** to ensure the same long URL always returns the same short link:

```
Long URL → SHA256 Hash → Partition Key (first 8 chars) + Row Key (full hash)
```

This provides:
- **O(1) lookups** for existing URLs
- **Storage efficiency** by avoiding duplicate entries
- **Hash collision handling** by storing the original URL for verification

---

## Data Model

### Table: ShortUrls (Primary)

Stores the mapping from short alias to long URL.

| Property | Type | Description |
|----------|------|-------------|
| `PartitionKey` | string | First 2 characters of alias (lowercase) |
| `RowKey` | string | The alias itself |
| `LongUrl` | string | Original URL to redirect to |
| `CreatedAtUtc` | DateTimeOffset | Creation timestamp |
| `ExpiresAtUtc` | DateTimeOffset? | Expiration time (null = never) |
| `IsDisabled` | bool | Manual disable flag |
| `CreatedBy` | string? | Creator identifier (future use) |

**Partition Key Strategy:**
```
Alias: "abc123" → PartitionKey: "ab"
```
- Distributes data across ~3,844 partitions (62²)
- Prevents hot partitions for high-throughput scenarios

### Table: UrlIndex (Deduplication)

Reverse lookup from long URL hash to alias.

| Property | Type | Description |
|----------|------|-------------|
| `PartitionKey` | string | First 8 characters of URL hash |
| `RowKey` | string | Full SHA256 hash of URL |
| `Alias` | string | The short alias |
| `LongUrl` | string | Original URL (for collision verification) |
| `ExpiresAtUtc` | DateTimeOffset? | When this entry expires |

### Table: ShortUrlsExpiryIndex (Garbage Collection)

Time-based index for efficient expired link cleanup.

| Property | Type | Description |
|----------|------|-------------|
| `PartitionKey` | string | Expiry date (yyyyMMdd format) |
| `RowKey` | string | Time + alias (HHmmss\|alias format) |
| `AliasPartitionKey` | string | Primary table partition key |
| `AliasRowKey` | string | Primary table row key (alias) |
| `ExpiresAtUtc` | DateTimeOffset | Exact expiration time |

**Partition Key Strategy:**
```
ExpiresAt: 2025-12-27T14:30:00Z → PartitionKey: "20251227"
                                → RowKey: "143000|abc123"
```

---

## API Reference

### Health Check

```http
GET /api/health
```

**Response:** `200 OK`
```json
{
  "status": "healthy",
  "timestamp": "2025-12-20T10:30:00Z"
}
```

### Shorten URL

```http
POST /api/shorten
Content-Type: application/json
```

**Request Body:**
```json
{
  "longUrl": "https://example.com/very/long/url/path?query=params",
  "expiresInSeconds": 604800
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `longUrl` | string | Yes | URL to shorten (http/https, max 4096 chars) |
| `expiresInSeconds` | int | No | TTL in seconds (60 - 7776000) |

**Success Response (New URL):** `201 Created`
```json
{
  "alias": "x7Kp2m",
  "shortUrl": "https://tiney.to/x7Kp2m",
  "longUrl": "https://example.com/very/long/url/path?query=params",
  "createdAtUtc": "2025-12-20T10:30:00Z",
  "expiresAtUtc": "2025-12-27T10:30:00Z"
}
```

**Success Response (Existing URL):** `200 OK`
```json
{
  "alias": "x7Kp2m",
  "shortUrl": "https://tiney.to/x7Kp2m",
  "longUrl": "https://example.com/very/long/url/path?query=params",
  "createdAtUtc": "2025-12-15T10:30:00Z",
  "expiresAtUtc": "2025-12-22T10:30:00Z"
}
```

**Error Responses:**

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid request body or validation error |
| `500 Internal Server Error` | Failed to generate unique alias after retries |

### Redirect

```http
GET /{alias}
```

| Status | Condition |
|--------|-----------|
| `302 Found` | Valid alias, redirects to long URL |
| `404 Not Found` | Alias doesn't exist or invalid format |
| `410 Gone` | Alias expired or disabled |

---

## Caching Strategy

### In-Memory Cache (Per-Instance)

The `CachingShortUrlRepository` implements a decorator pattern to add caching:

```
┌─────────────────────────────────────────────────────────┐
│              CachingShortUrlRepository                  │
│  ┌───────────────────────────────────────────────────┐  │
│  │            In-Memory LRU Cache                    │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────┐ │  │
│  │  │ shorturl:   │  │ shorturl:   │  │ shorturl: │ │  │
│  │  │   abc123    │  │   xyz789    │  │   NOTFND  │ │  │
│  │  └─────────────┘  └─────────────┘  └───────────┘ │  │
│  └───────────────────────────────────────────────────┘  │
│                          │                              │
│                          ▼                              │
│  ┌───────────────────────────────────────────────────┐  │
│  │           TableShortUrlRepository                 │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### Cache Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `CACHE_SIZE_MB` | 50 | Maximum cache size in MB |
| `CACHE_DURATION_SECONDS` | 300 | Cache entry TTL (5 minutes) |
| Negative Cache TTL | 60s | TTL for "not found" entries |

### Cache Behavior

| Scenario | Behavior |
|----------|----------|
| **Cache Hit (Found)** | Return cached entity immediately |
| **Cache Hit (Not Found)** | Return null immediately (negative cache) |
| **Cache Miss** | Query storage, cache result, return |
| **Insert** | Add to cache after successful storage insert |
| **Delete** | Remove from cache after storage delete |

### Cache Expiration Logic

The cache respects URL expiration times:
```csharp
// Cache duration is minimum of:
// 1. Configured cache duration (default 5 min)
// 2. Time until URL expires
var effectiveDuration = entity.ExpiresAtUtc.HasValue
    ? Min(cacheDuration, entity.ExpiresAtUtc - now)
    : cacheDuration;
```

---

## Expiry & Garbage Collection

### Expired Link Reaper

The `ExpiredLinkReaperFunction` runs on a timer trigger to clean up expired entries.

```
┌────────────────────────────────────────────────────────────────┐
│                    Expired Link Reaper                         │
│  Timer Trigger: Every 15 minutes (0 */15 * * * *)              │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│  1. Acquire Blob Lease (Distributed Lock)                      │
│     └── If failed (409 Conflict): Exit (another instance runs) │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│  2. Scan Expiry Index (yesterday + today partitions)           │
│     └── Query: ExpiresAtUtc < now                              │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│  3. For each expired entry:                                    │
│     ├── Delete from ShortUrls table                            │
│     ├── Delete from UrlIndex table                             │
│     └── Delete from ExpiryIndex table                          │
└────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌────────────────────────────────────────────────────────────────┐
│  4. Release Blob Lease                                         │
└────────────────────────────────────────────────────────────────┘
```

### Distributed Locking

Uses Azure Blob Lease for distributed locking:

| Property | Value |
|----------|-------|
| Lock Type | Blob Lease (60 seconds) |
| Lock Blob | `locks/expiry-reaper.lock` |
| Behavior | Only one instance can run GC at a time |

---

## Scalability & Performance

### Performance Characteristics

| Operation | Latency | Bottleneck |
|-----------|---------|------------|
| Redirect (cache hit) | < 10ms | In-memory lookup |
| Redirect (cache miss) | 20-50ms | Table Storage query |
| Shorten (new URL) | 50-150ms | 3 Table Storage writes |
| Shorten (existing) | 20-50ms | 1 Table Storage read |

### Scaling Dimensions

| Dimension | Strategy |
|-----------|----------|
| **Compute** | Azure Functions auto-scales based on queue length and HTTP load |
| **Storage** | Table Storage scales automatically, partition key design prevents hotspots |
| **Cache** | Per-instance cache; more instances = more total cache capacity |

### Capacity Planning

```
Alias Capacity (6 chars):  56.8 billion unique aliases
Storage Cost Estimate:     ~$0.045/GB/month for Table Storage
Function Cost:             ~$0.20 per million executions

Example: 1M redirects/day
- Cache hit rate ~80% → 200K storage reads/day
- Table Storage reads: ~$0.01/day
- Function executions: ~$0.20/day
- Total: ~$6.30/month
```

---

## Security Considerations

### Input Validation

| Validation | Rule |
|------------|------|
| URL Scheme | Only `http` and `https` allowed |
| URL Length | Maximum 4,096 characters |
| Alias Format | `^[A-Za-z0-9_-]{1,32}$` (regex validated) |
| TTL Range | 60 seconds to 7,776,000 seconds (90 days) |

### Security Features

| Feature | Implementation |
|---------|----------------|
| HTTPS Only | Enforced at Function App level |
| No URL Logging | Long URLs not logged (privacy) |
| Rate Limiting | (Future) Azure Front Door or APIM |
| Authentication | (Future) API key or OAuth |

### Threat Mitigations

| Threat | Mitigation |
|--------|------------|
| Open Redirect | URL validation (http/https only) |
| Alias Enumeration | Random Base62 aliases (high entropy) |
| Storage Exhaustion | TTL limits, GC cleanup |
| Hot Partition DoS | Distributed partition key strategy |

---

## Infrastructure

### Azure Resources (Bicep)

```
┌─────────────────────────────────────────────────────────────┐
│                     Resource Group                          │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Storage Account (Standard LRS)                          ││
│  │ ├── Table: ShortUrls                                    ││
│  │ ├── Table: ShortUrlsExpiryIndex                         ││
│  │ ├── Table: UrlIndex                                     ││
│  │ └── Container: locks                                    ││
│  └─────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────┐│
│  │ App Service Plan (Consumption Y1)                       ││
│  │ └── Function App (dotnet-isolated, .NET 8)              ││
│  └─────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Log Analytics Workspace                                 ││
│  │ └── Application Insights                                ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

### Resource Configuration

| Resource | SKU | Notes |
|----------|-----|-------|
| Storage Account | Standard_LRS | Locally redundant, sufficient for most use cases |
| App Service Plan | Y1 (Dynamic) | Consumption-based pricing |
| Application Insights | Per-GB | 30-day retention |

---

## Project Structure

```
tiney/
├── README.md                          # This document
├── tiney.sln                          # Solution file
├── infra/                             # Infrastructure as Code
│   ├── main.bicep                     # Azure resources definition
│   ├── parameters.dev.json            # Development parameters
│   ├── parameters.prod.json           # Production parameters
│   └── deploy.ps1                     # Deployment script
│
└── src/
    ├── TineyTo.Functions/             # Main application
    │   ├── Program.cs                 # Host configuration & DI
    │   ├── host.json                  # Functions host settings
    │   ├── local.settings.json        # Local dev settings
    │   │
    │   ├── Functions/                 # Azure Function triggers
    │   │   ├── HealthFunction.cs      # GET /api/health
    │   │   ├── ShortenFunction.cs     # POST /api/shorten
    │   │   ├── RedirectFunction.cs    # GET /{alias}
    │   │   └── ExpiredLinkReaperFunction.cs  # Timer trigger
    │   │
    │   ├── Models/                    # Request/Response DTOs
    │   │   ├── ShortenRequest.cs
    │   │   └── ShortenResponse.cs
    │   │
    │   ├── Services/                  # Business logic
    │   │   ├── AliasGenerator.cs      # Random alias generation
    │   │   ├── UrlValidation.cs       # Input validation
    │   │   └── TimeProvider.cs        # Abstraction for testing
    │   │
    │   └── Storage/                   # Data access layer
    │       ├── IShortUrlRepository.cs
    │       ├── TableShortUrlRepository.cs
    │       ├── CachingShortUrlRepository.cs
    │       ├── IExpiryIndexRepository.cs
    │       ├── TableExpiryIndexRepository.cs
    │       ├── IUrlIndexRepository.cs
    │       ├── TableUrlIndexRepository.cs
    │       └── Entities/
    │           ├── ShortUrlEntity.cs
    │           ├── ExpiryIndexEntity.cs
    │           └── UrlIndexEntity.cs
    │
    └── TineyTo.Functions.Tests/       # Unit tests
        ├── Functions/                 # Function tests
        ├── Services/                  # Service tests
        ├── Storage/                   # Repository tests
        └── Mocks/                     # Test doubles
```

---

## Getting Started

### Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build and run |
| Azure Functions Core Tools | 4.x | Local development |
| Azurite | Latest | Local storage emulator |
| Node.js | 18+ | For Azurite (npm) |

### 1. Clone and Build

```powershell
git clone https://github.com/your-org/tiney.git
cd tiney
dotnet build
```

### 2. Start Azurite (Storage Emulator)

```powershell
# Option 1: If installed globally
azurite --silent --location .azurite --debug .azurite/debug.log

# Option 2: Using npx
npx azurite --silent --location .azurite --debug .azurite/debug.log
```

### 3. Start the Functions App

```powershell
cd src/TineyTo.Functions
func start
```

The app will be available at `http://localhost:7071`

### 4. Test the API

```powershell
# Health check
curl http://localhost:7071/api/health

# Shorten a URL
curl -X POST http://localhost:7071/api/shorten `
  -H "Content-Type: application/json" `
  -d '{"longUrl": "https://example.com/test"}'

# Redirect (use the alias from the response)
curl -I http://localhost:7071/{alias}
```

---

## Configuration Reference

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `AzureWebJobsStorage` | Azure Storage connection (set by Azure) | `UseDevelopmentStorage=true` |
| `TABLE_CONNECTION` | Azure Storage connection string | Falls back to `AzureWebJobsStorage` |
| `SHORTURL_TABLE_NAME` | Primary table name | `ShortUrls` |
| `EXPIRYINDEX_TABLE_NAME` | Expiry index table name | `ShortUrlsExpiryIndex` |
| `URLINDEX_TABLE_NAME` | URL index table name | `UrlIndex` |
| `GC_BLOB_LOCK_CONNECTION` | Blob storage connection for locks | Falls back to `AzureWebJobsStorage` |
| `GC_BLOB_LOCK_CONTAINER` | Blob container for locks | `locks` |
| `GC_BLOB_LOCK_BLOB` | Lock blob name | `expiry-reaper.lock` |
| `BaseUrl` | Base URL for short links | Required in Azure |
| `DefaultTtlDays` | Default link expiration in days | `30` |
| `MaxTtlDays` | Maximum allowed TTL in days | `365` |
| `CACHE_SIZE_MB` | In-memory cache size limit | `50` |
| `CACHE_DURATION_SECONDS` | Cache entry TTL | `300` |

**Note**: In Azure deployments, if `TABLE_CONNECTION` or `GC_BLOB_LOCK_CONNECTION` are not explicitly set, the application will automatically use `AzureWebJobsStorage` for backward compatibility.

### local.settings.json Example

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "TABLE_CONNECTION": "UseDevelopmentStorage=true",
    "SHORT_BASE_URL": "http://localhost:7071",
    "ALIAS_LENGTH": "6",
    "CACHE_SIZE_MB": "50",
    "CACHE_DURATION_SECONDS": "300",
    "MAX_TTL_SECONDS": "7776000"
  }
}
```

---

## Testing

### Run Unit Tests

```powershell
cd src/TineyTo.Functions.Tests
dotnet test
```

### Run with Coverage

```powershell
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

| Category | Description |
|----------|-------------|
| `Functions/` | HTTP trigger and timer function tests |
| `Services/` | Business logic tests (validation, alias generation) |
| `Storage/` | Repository and entity tests |
| `Mocks/` | Reusable test doubles |

---

## Deployment

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- Azure subscription with appropriate permissions

### Build & Package

The deployment process builds for **32-bit (win-x86)** architecture to support the **Azure Functions F1 Free Tier**.

```powershell
# Build and create deployment package
cd src/TineyTo.Functions
dotnet publish --configuration Release --runtime win-x86 --self-contained false --output ..\..\publish

# Create deployment zip
cd ..\..
Compress-Archive -Path "publish\*" -DestinationPath "publish.zip" -Force
```

### Deploy Infrastructure & Application

```powershell
cd infra

# Deploy to development (F1 Free Tier in Canada Central)
.\deploy.ps1 -Environment dev -ResourceGroupName rg-tiney-dev

# Deploy to production
.\deploy.ps1 -Environment prod -ResourceGroupName rg-tiney-prod
```

The deployment script will:
1. Create/update Azure resources (Storage Account, Function App, App Insights)
2. Build the application for 32-bit architecture
3. Package the deployment
4. Deploy to Azure Functions

### Manual Deployment (if needed)

```powershell
# After building the package
az functionapp deployment source config-zip \
  --resource-group rg-tiney-dev \
  --name func-tiney-dev-<uniqueSuffix> \
  --src publish.zip
```

### Infrastructure Configuration

The Bicep template (`infra/main.bicep`) configures:
- **Region**: Canada Central (default)
- **App Service Plan**: F1 Free Tier (32-bit)
- **Runtime**: .NET 8 Isolated Worker
- **Storage**: Standard_LRS with Tables and Blob containers
- **Monitoring**: Application Insights with Log Analytics

### Important Notes

- **32-bit Architecture**: The F1 Free Tier requires 32-bit builds (`win-x86`)
- **Connection Strings**: Automatically configured via Bicep deployment
- **First Deployment**: May take 2-3 minutes for the app to fully start
- **Storage Tables**: Created automatically on first run

### CI/CD Pipeline (GitHub Actions)

See `.github/workflows/` for:
- `build.yml` - Build and test on PR (must use win-x86 runtime)
- `deploy.yml` - Deploy to Azure on merge to main

---

## License

MIT License - See LICENSE file for details.
| `DefaultTtlDays` | Default link expiration | `30` |
| `MaxTtlDays` | Maximum allowed TTL | `365` |

## Architecture

- **Two-Table TTL Pattern**: Primary `ShortUrls` table partitioned by alias prefix, `ShortUrlsExpiryIndex` table partitioned by expiry date for efficient cleanup
- **Base62 Aliases**: 6-character aliases using `[0-9A-Za-z]` (56+ billion combinations)
- **Blob Lease Locking**: Prevents concurrent GC execution