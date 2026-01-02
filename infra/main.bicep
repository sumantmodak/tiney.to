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

var uniqueSuffix = environmentName == 'prod' ? '783f57bd' : '549adabd'
var storageAccountName = 'storage-tiney-${environmentName}-${uniqueSuffix}'
var functionAppName = 'func-tiney-${environmentName}-${uniqueSuffix}'
var appServicePlanName = 'asp-tiney-${environmentName}-${uniqueSuffix}'
var appInsightsName = 'appi-tiney-${environmentName}-${uniqueSuffix}'
var logAnalyticsName = 'log-tiney-${environmentName}-${uniqueSuffix}'

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

// App Service Plan (Free tier)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: false // false for Windows
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: true
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
      ]
    }
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output appInsightsName string = appInsights.name
