@description('The location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
param environmentName string = 'prod'

@description('Base URL for short links (e.g., https://tiney.to)')
param baseUrl string

@description('Length of generated aliases')
param aliasLength int = 6

@description('Maximum TTL in seconds for shortened URLs (default: 7776000 = 90 days)')
param maxTtlSeconds int = 7776000

@description('Enable in-memory caching')
param cacheEnabled bool = true

@description('Cache size limit in MB')
param cacheSizeMb int = 10

@description('Default cache duration in seconds (15 minutes)')
param cacheDurationSeconds int = 900

@description('Negative cache duration in seconds (1 minute)')
param cacheNegativeSeconds int = 60

@description('Enable rate limiting')
param rateLimitEnabled bool = true

@description('Max shorten requests per URL within the time window')
param rateLimitShortenPerUrl int = 5

@description('Time window in seconds for per-URL shorten rate limit')
param rateLimitShortenPerUrlWindow int = 60

@description('Max shorten requests per IP within the time window')
param rateLimitShortenPerIp int = 10

@description('Time window in seconds for per-IP shorten rate limit')
param rateLimitShortenPerIpWindow int = 60

@description('Max redirect requests per alias within the time window')
param rateLimitRedirectPerAlias int = 100

@description('Time window in seconds for per-alias redirect rate limit')
param rateLimitRedirectPerAliasWindow int = 10

@description('Max redirect requests per IP within the time window')
param rateLimitRedirectPerIp int = 60

@description('Time window in seconds for per-IP redirect rate limit')
param rateLimitRedirectPerIpWindow int = 60

@description('Max 404 responses per IP within the time window')
param rateLimitNotFoundPerIp int = 20

@description('Time window in seconds for 404 rate limit')
param rateLimitNotFoundPerIpWindow int = 60

@description('Enable API key authentication for shorten endpoint')
param apiAuthEnabled bool = true

@description('Comma-separated list of valid API keys for shorten endpoint')
@secure()
param shortenApiKeys string

@description('Name of the Azure Storage Queue for statistics events')
param statisticsQueueName string = 'statistics-events'

var uniqueSuffix = environmentName == 'prod' ? '783f57bd' : '549adabd'
var storageAccountName = 'tineystash${uniqueSuffix}'
var functionAppName = 'tiney-swiftlink-${environmentName}-${uniqueSuffix}'
var appServicePlanName = 'appservice-turbohub-${environmentName}-${uniqueSuffix}'
var appInsightsName = 'tiney-eyewatch-${environmentName}-${uniqueSuffix}'
var logAnalyticsName = 'tiney-swiftlogs-${environmentName}-${uniqueSuffix}'

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

// Table Service
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

// Tables
resource shortUrlsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableService
  name: 'ShortUrls'
}

resource expiryIndexTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableService
  name: 'ShortUrlsExpiryIndex'
}

resource urlIndexTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableService
  name: 'UrlIndex'
}

// Blob Service (for GC lease lock)
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource locksContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: 'locks'
  properties: {
    publicAccess: 'None'
  }
}

// App Service Plan (Linux Basic tier for Production)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true // true for Linux
  }
}

// Function App (Linux)
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'SHORT_BASE_URL'
          value: baseUrl
        }
        {
          name: 'ALIAS_LENGTH'
          value: string(aliasLength)
        }
        {
          name: 'MAX_TTL_SECONDS'
          value: string(maxTtlSeconds)
        }
        {
          name: 'TABLE_CONNECTION'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'SHORTURL_TABLE_NAME'
          value: 'ShortUrls'
        }
        {
          name: 'EXPIRYINDEX_TABLE_NAME'
          value: 'ShortUrlsExpiryIndex'
        }
        {
          name: 'URLINDEX_TABLE_NAME'
          value: 'UrlIndex'
        }
        {
          name: 'GC_BLOB_LOCK_CONNECTION'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'GC_BLOB_LOCK_CONTAINER'
          value: 'locks'
        }
        {
          name: 'GC_BLOB_LOCK_BLOB'
          value: 'expiry-reaper.lock'
        }
        {
          name: 'CACHE_ENABLED'
          value: string(cacheEnabled)
        }
        {
          name: 'CACHE_SIZE_MB'
          value: string(cacheSizeMb)
        }
        {
          name: 'CACHE_DURATION_SECONDS'
          value: string(cacheDurationSeconds)
        }
        {
          name: 'CACHE_NEGATIVE_SECONDS'
          value: string(cacheNegativeSeconds)
        }
        {
          name: 'RATE_LIMIT_ENABLED'
          value: string(rateLimitEnabled)
        }
        {
          name: 'RATE_LIMIT_SHORTEN_PER_URL'
          value: string(rateLimitShortenPerUrl)
        }
        {
          name: 'RATE_LIMIT_SHORTEN_PER_URL_WINDOW'
          value: string(rateLimitShortenPerUrlWindow)
        }
        {
          name: 'RATE_LIMIT_SHORTEN_PER_IP'
          value: string(rateLimitShortenPerIp)
        }
        {
          name: 'RATE_LIMIT_SHORTEN_PER_IP_WINDOW'
          value: string(rateLimitShortenPerIpWindow)
        }
        {
          name: 'RATE_LIMIT_REDIRECT_PER_ALIAS'
          value: string(rateLimitRedirectPerAlias)
        }
        {
          name: 'RATE_LIMIT_REDIRECT_PER_ALIAS_WINDOW'
          value: string(rateLimitRedirectPerAliasWindow)
        }
        {
          name: 'RATE_LIMIT_REDIRECT_PER_IP'
          value: string(rateLimitRedirectPerIp)
        }
        {
          name: 'RATE_LIMIT_REDIRECT_PER_IP_WINDOW'
          value: string(rateLimitRedirectPerIpWindow)
        }
        {
          name: 'RATE_LIMIT_404_PER_IP'
          value: string(rateLimitNotFoundPerIp)
        }
        {
          name: 'RATE_LIMIT_404_PER_IP_WINDOW'
          value: string(rateLimitNotFoundPerIpWindow)
        }
        {
          name: 'API_AUTH_ENABLED'
          value: string(apiAuthEnabled)
        }
        {
          name: 'SHORTEN_API_KEYS'
          value: shortenApiKeys
        }
        {
          name: 'STATISTICS_QUEUE_NAME'
          value: statisticsQueueName
        }
        {
          name: 'STATISTICS_QUEUE_CONNECTION'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
      ]
    }
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output appInsightsName string = appInsights.name
