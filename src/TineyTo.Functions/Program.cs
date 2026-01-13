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

        // Load all configurations from environment
        var appConfig = ApplicationConfiguration.LoadFromEnvironment();
        var storageConfig = StorageConfiguration.LoadFromEnvironment();
        var cacheConfig = CacheConfiguration.LoadFromEnvironment();
        var rateLimitConfig = RateLimitConfiguration.LoadFromEnvironment();
        var apiAuthConfig = ApiAuthConfiguration.LoadFromEnvironment();
        
        // Register configurations as singletons
        services.AddSingleton(appConfig);
        services.AddSingleton(storageConfig);
        services.AddSingleton(cacheConfig);
        services.AddSingleton(rateLimitConfig);
        services.AddSingleton(apiAuthConfig);

        // Table clients (singleton for connection reuse)
        var tableServiceClient = new TableServiceClient(storageConfig.TableConnection);
        
        var shortUrlTable = tableServiceClient.GetTableClient(storageConfig.ShortUrlTableName);
        shortUrlTable.CreateIfNotExists();
        services.AddSingleton(shortUrlTable);

        var expiryIndexTable = tableServiceClient.GetTableClient(storageConfig.ExpiryIndexTableName);
        expiryIndexTable.CreateIfNotExists();
        services.AddSingleton<TableClient>(sp => expiryIndexTable);

        var urlIndexTable = tableServiceClient.GetTableClient(storageConfig.UrlIndexTableName);
        urlIndexTable.CreateIfNotExists();

        // Blob client for GC locking
        var blobServiceClient = new BlobServiceClient(storageConfig.GcBlobLockConnection);
        var containerClient = blobServiceClient.GetBlobContainerClient(storageConfig.GcBlobLockContainer);
        containerClient.CreateIfNotExists();
        services.AddSingleton(containerClient);
        
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

        // Rate limiter - uses the same IMemoryCache instance
        services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
    })
    .Build();

host.Run();
