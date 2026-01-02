# Deploy tiney.to PROD infrastructure and application
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "canadacentral"
)

$ErrorActionPreference = "Stop"
$Environment = "prod"

Write-Host "Deploying tiney.to to PRODUCTION environment..." -ForegroundColor Red

# Create resource group if it doesn't exist
$rg = az group show --name $ResourceGroupName 2>$null | ConvertFrom-Json
if (-not $rg) {
    Write-Host "Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location
}

# Deploy infrastructure
Write-Host "Deploying infrastructure..." -ForegroundColor Yellow
$deployment = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file "$PSScriptRoot/main.bicep" `
    --parameters "$PSScriptRoot/parameters.json" `
    --query "properties.outputs" `
    --output json | ConvertFrom-Json

$functionAppName = $deployment.functionAppName.value
Write-Host "Function App: $functionAppName" -ForegroundColor Green

# Build and publish the Functions app
Write-Host "Building application..." -ForegroundColor Yellow
$publishPath = "$PSScriptRoot/../../publish"
dotnet publish "$PSScriptRoot/../../src/TineyTo.Functions/TineyTo.Functions.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $publishPath

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Yellow
$zipPath = Resolve-Path "$PSScriptRoot/../../publish.zip" -ErrorAction SilentlyContinue
if ($zipPath -and (Test-Path $zipPath)) { Remove-Item $zipPath -Force }
$zipPath = Join-Path (Resolve-Path "$PSScriptRoot/../..") "publish.zip"

# Include all files including hidden ones (like .azurefunctions)
$filesToZip = Get-ChildItem -Path $publishPath -Recurse -Force | Where-Object { !$_.PSIsContainer } | Select-Object -ExpandProperty FullName
Compress-Archive -Path $filesToZip -DestinationPath $zipPath -Force

Write-Host "Package created: $zipPath ($(((Get-Item $zipPath).Length / 1MB).ToString('0.00')) MB)" -ForegroundColor Green

# Deploy to Azure
Write-Host "Deploying to Azure Functions..." -ForegroundColor Yellow
$deployResult = az functionapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $functionAppName `
    --src $zipPath `
    2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment failed: $deployResult" -ForegroundColor Red
    exit 1
}

Write-Host "Deployment completed successfully" -ForegroundColor Green

# Cleanup
Remove-Item $publishPath -Recurse -Force
Remove-Item $zipPath -Force

Write-Host ""
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "Function App URL: https://$functionAppName.azurewebsites.net/api/health" -ForegroundColor Cyan
