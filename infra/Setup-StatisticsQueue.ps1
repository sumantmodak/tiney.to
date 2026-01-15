Write-Host 'Setting up Statistics Queue...' -ForegroundColor Cyan
Write-Host 'Queue Name: statistics-events' -ForegroundColor Gray
Write-Host ''
Write-Host 'Local Development Configuration' -ForegroundColor Yellow
Write-Host 'The queue will be created automatically when first accessed by the Function App' -ForegroundColor Gray
Write-Host ''
try {
    $null = Invoke-WebRequest -Uri 'http://127.0.0.1:10001/' -Method GET -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop
    Write-Host ' Azurite Queue Service is running' -ForegroundColor Green
} catch {
    Write-Host ' Azurite may not be running. Start with: azurite' -ForegroundColor Yellow
}
Write-Host ''
Write-Host ' Configuration complete. Queue will be auto-created on first use.' -ForegroundColor Green
Write-Host ''
