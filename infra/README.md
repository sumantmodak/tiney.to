# Infrastructure

This folder contains the infrastructure configuration for deploying Tiney.to to Azure.

## Structure

```
infra/
└── buildout/               # Production environment
    ├── main.bicep          # Infrastructure template
    ├── parameters.json     # Configuration parameters
    └── deploy.ps1          # Deployment script
```

## Prerequisites

- Azure CLI installed
- Azure subscription with appropriate permissions
- PowerShell 7+
- Generated API keys (use `scripts/Generate-ApiKey.ps1`)

## Configuration

Before deploying, update `buildout/parameters.json`:

1. **API Authentication**: Replace `"REPLACE_WITH_GENERATED_API_KEYS"` with your generated API keys (comma-separated)
2. **Base URL**: Set your custom domain or use the Azure Functions URL
3. **Resource limits**: Adjust cache size, TTL, and rate limits as needed

### Key Parameters

- **apiAuthEnabled**: Enable/disable API key authentication (default: true)
- **shortenApiKeys**: Comma-separated list of valid API keys for shorten endpoint
- **baseUrl**: The base URL for short links (e.g., `https://tiney.to`)
- **maxTtlSeconds**: Maximum TTL for shortened URLs (default: 2592000 = 30 days)
- **cacheEnabled**: Enable in-memory caching (default: true)
- **rateLimitEnabled**: Enable rate limiting (default: true)

## Deployment

```powershell
cd infra/buildout
./deploy.ps1 -ResourceGroupName "rg-tiney-prod" -Location "canadacentral"
```

The deployment script will:
1. Create or update the resource group
2. Deploy the Bicep template with parameters
3. Output the Function App name, URL, storage account, and Application Insights details

## Resources Created

- **Azure Functions**: .NET 8 isolated worker for URL shortening logic
- **Storage Account**: Azure Table Storage for URL mappings and indices
- **App Service Plan**: Hosting plan for the Function App
- **Application Insights**: Monitoring and diagnostics
- **Log Analytics Workspace**: Log aggregation and analysis

## Post-Deployment

1. **Static Web App**: Configure the SWA with:
   - `BACKEND_URL`: The deployed Function App URL
   - `BACKEND_API_KEY`: One of the API keys from `shortenApiKeys`

2. **Custom Domain**: Configure your custom domain in Azure
3. **Monitoring**: Set up alerts in Application Insights
4. **Testing**: Verify the deployment with test requests

## Security

- API keys are marked as `@secure()` parameters in Bicep
- Store API keys in Azure Key Vault for production
- Rotate API keys regularly using the generation script
- Enable HTTPS-only traffic (configured by default)

