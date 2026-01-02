# Infrastructure Organization

This folder contains separate infrastructure configurations for different environments.

## Structure

```
infra/
├── dev/                    # Development environment
│   ├── main.bicep          # Dev infrastructure template
│   ├── parameters.json     # Dev parameters
│   └── deploy.ps1          # Dev deployment script
│
└── prod/                   # Production environment
    ├── main.bicep          # Prod infrastructure template
    ├── parameters.json     # Prod parameters
    └── deploy.ps1          # Prod deployment script
```

## Deployment

### Deploy to Development

```powershell
cd infra/dev
./deploy.ps1 -ResourceGroupName "rg-tiney-dev" -Location "canadacentral"
```

### Deploy to Production

```powershell
cd infra/prod
./deploy.ps1 -ResourceGroupName "rg-tiney-prod" -Location "canadacentral"
```

## Environment Differences

### Development
- **baseUrl**: `https://func-tiney-dev-549adabd.azurewebsites.net`
- **maxTtlSeconds**: 2592000 (30 days)
- **cacheSizeMb**: 10 MB
- **SKU**: Free tier (F1)

### Production
- **baseUrl**: `https://tiney.to`
- **maxTtlSeconds**: 2592000 (30 days)
- **cacheSizeMb**: 50 MB
- **SKU**: Can be upgraded to higher tier

## Customizing Infrastructure

Each environment has its own `main.bicep` and `parameters.json` files, allowing you to:
- Use different Azure resource SKUs
- Configure different networking setups
- Apply environment-specific security policies
- Set different scaling configurations

Make changes in the appropriate environment folder without affecting other environments.
