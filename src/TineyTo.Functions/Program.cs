using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configuration
        // Use AzureWebJobsStorage as fallback (set by Azure Functions)
        var azureWebJobsStorage = Environment.GetEnvironmentVariable("AzureWebJobsStorage") 
            ?? "UseDevelopmentStorage=true";
        
        var tableConnection = Environment.GetEnvironmentVariable("TABLE_CONNECTION") 
            ?? azureWebJobsStorage;
        var shortUrlTableName = Environment.GetEnvironmentVariable("SHORTURL_TABLE_NAME") 
            ?? "ShortUrls";
        var expiryIndexTableName = Environment.GetEnvironmentVariable("EXPIRYINDEX_TABLE_NAME") 
            ?? "ShortUrlsExpiryIndex";
        var urlIndexTableName = Environment.GetEnvironmentVariable("URLINDEX_TABLE_NAME") 
            ?? "UrlIndex";
        var gcBlobConnection = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONNECTION") 
            ?? azureWebJobsStorage;
        var gcBlobContainer = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONTAINER") 
            ?? "locks";

        // Table clients (singleton for connection reuse)
        var tableServiceClient = new TableServiceClient(tableConnection);
        
        var shortUrlTable = tableServiceClient.GetTableClient(shortUrlTableName);
        shortUrlTable.CreateIfNotExists();
        services.AddSingleton(shortUrlTable);

        var expiryIndexTable = tableServiceClient.GetTableClient(expiryIndexTableName);
        expiryIndexTable.CreateIfNotExists();
        services.AddSingleton<TableClient>(sp => expiryIndexTable);

        var urlIndexTable = tableServiceClient.GetTableClient(urlIndexTableName);
        urlIndexTable.CreateIfNotExists();

        // Blob client for GC locking
        var blobServiceClient = new BlobServiceClient(gcBlobConnection);
        var containerClient = blobServiceClient.GetBlobContainerClient(gcBlobContainer);
        containerClient.CreateIfNotExists();
        services.AddSingleton(containerClient);

        // Cache configuration
        var cacheConfig = CacheConfiguration.LoadFromEnvironment();
        services.AddSingleton(cacheConfig);
        
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = cacheConfig.SizeLimitMb * 1024; // Approximate entries (1KB each)
        });

        // Repositories - wrap with caching decorator if enabled
        services.AddSingleton<IShortUrlRepository>(sp =>
        {
            var inner = new TableShortUrlRepository(shortUrlTable);
            if (cacheConfig.IsEnabled)
            {
                return new CachingShortUrlRepository(
                    inner,
                    sp.GetRequiredService<IMemoryCache>(),
                    cacheConfig,
                    sp.GetRequiredService<ITimeProvider>(),
                    sp.GetRequiredService<ICacheMetrics>());
            }
            return inner;
        });
        services.AddSingleton<IExpiryIndexRepository>(sp => 
            new TableExpiryIndexRepository(expiryIndexTable));
        services.AddSingleton<IUrlIndexRepository>(sp => 
            new TableUrlIndexRepository(urlIndexTable));

        // Services
        services.AddSingleton<IAliasGenerator, AliasGenerator>();
        services.AddSingleton<IUrlValidator, UrlValidator>();
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<ICacheMetrics, LoggingCacheMetrics>();
    })
    .Build();

host.Run();
