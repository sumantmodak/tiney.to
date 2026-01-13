# Deploy tiney.to infrastructure only (for GitHub Actions integration)
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "canadacentral"
)

$ErrorActionPreference = "Stop"

Write-Host "Deploying tiney.to infrastructure..." -ForegroundColor Yellow

# Create resource group if it doesn't exist
$rg = az group show --name $ResourceGroupName 2>$null | ConvertFrom-Json
if (-not $rg) {
    Write-Host "Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location
}

# Deploy infrastructure
Write-Host "Deploying infrastructure via Bicep..." -ForegroundColor Yellow
$deployment = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file "$PSScriptRoot/main.bicep" `
    --parameters "$PSScriptRoot/parameters.json" `
    --query "properties.outputs" `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Infrastructure deployment failed" -ForegroundColor Red
    exit 1
}

$functionAppName = $deployment.functionAppName.value
$functionAppUrl = $deployment.functionAppUrl.value
$storageAccountName = $deployment.storageAccountName.value
$appInsightsName = $deployment.appInsightsName.value

Write-Host ""
Write-Host "Infrastructure deployment complete!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Function App Name:    $functionAppName" -ForegroundColor White
Write-Host "Function App URL:     $functionAppUrl" -ForegroundColor White
Write-Host "Storage Account:      $storageAccountName" -ForegroundColor White
Write-Host "Application Insights: $appInsightsName" -ForegroundColor White
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Configure GitHub Actions to deploy the Function App" -ForegroundColor White
Write-Host "2. Set up deployment credentials for: $functionAppName" -ForegroundColor White
Write-Host "3. Use 'az functionapp deployment list-publishing-credentials' to get publish profile" -ForegroundColor White
