<#
.SYNOPSIS
    Generates cryptographically secure API keys for Tiney.to API authentication.

.DESCRIPTION
    Creates Base64-encoded random strings suitable for use as API keys.
    Uses .NET's cryptographically secure random number generator.

.PARAMETER Count
    The number of API keys to generate (default: 5).

.PARAMETER Length
    The number of random bytes to generate per key (default: 32).
    The resulting Base64 string will be longer due to encoding.

.EXAMPLE
    .\Generate-ApiKey.ps1
    Generates 5 API keys with 32 bytes each

.EXAMPLE
    .\Generate-ApiKey.ps1 -Count 10
    Generates 10 API keys

.EXAMPLE
    .\Generate-ApiKey.ps1 -Count 3 -Length 64
    Generates 3 API keys with 64 bytes each
#>

param(
    [int]$Count = 5,
    [int]$Length = 32
)

# Generate cryptographically secure random bytes
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$apiKeys = @()

for ($i = 1; $i -le $Count; $i++) {
    $bytes = New-Object byte[] $Length
    $rng.GetBytes($bytes)
    $apiKey = [Convert]::ToBase64String($bytes)
    $apiKeys += $apiKey
    
    Write-Host "Key $i`: " -ForegroundColor Green -NoNewline
    Write-Host $apiKey -ForegroundColor Cyan
}

$rng.Dispose()

# Join all keys with commas for configuration
$commaSeparated = $apiKeys -join ','

Write-Host "`n" + ("=" * 80) -ForegroundColor DarkGray
Write-Host "`nAdd to local.settings.json:" -ForegroundColor Yellow
Write-Host @"
{
  "Values": {
    "API_AUTH_ENABLED": "true",
    "SHORTEN_API_KEYS": "$commaSeparated"
  }
}
"@ -ForegroundColor Gray

Write-Host "`nAdd to Azure App Settings:" -ForegroundColor Yellow
Write-Host "API_AUTH_ENABLED=true" -ForegroundColor Gray
Write-Host "SHORTEN_API_KEYS=$commaSeparated" -ForegroundColor Gray

Write-Host "`nUse any key in HTTP requests:" -ForegroundColor Yellow
Write-Host "X-API-Key: $($apiKeys[0])" -ForegroundColor Gray
Write-Host ""
