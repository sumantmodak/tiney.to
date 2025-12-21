using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configuration
        var tableConnection = Environment.GetEnvironmentVariable("TABLE_CONNECTION") 
            ?? "UseDevelopmentStorage=true";
        var shortUrlTableName = Environment.GetEnvironmentVariable("SHORTURL_TABLE_NAME") 
            ?? "ShortUrls";
        var expiryIndexTableName = Environment.GetEnvironmentVariable("EXPIRYINDEX_TABLE_NAME") 
            ?? "ShortUrlsExpiryIndex";
        var urlIndexTableName = Environment.GetEnvironmentVariable("URLINDEX_TABLE_NAME") 
            ?? "UrlIndex";
        var gcBlobConnection = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONNECTION") 
            ?? "UseDevelopmentStorage=true";
        var gcBlobContainer = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONTAINER") 
            ?? "tiney-locks";

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

        // In-memory cache for frequently accessed URLs
        var cacheSizeMb = int.TryParse(
            Environment.GetEnvironmentVariable("CACHE_SIZE_MB"), out var size) ? size : 50;
        var cacheDurationSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("CACHE_DURATION_SECONDS"), out var duration) ? duration : 300;
        
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = cacheSizeMb * 1024; // Approximate entries (1KB each)
        });

        // Repositories (with caching decorator)
        services.AddSingleton<TableShortUrlRepository>(sp => 
            new TableShortUrlRepository(shortUrlTable));
        services.AddSingleton<IShortUrlRepository>(sp =>
        {
            var innerRepo = sp.GetRequiredService<TableShortUrlRepository>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<CachingShortUrlRepository>>();
            return new CachingShortUrlRepository(
                innerRepo, 
                cache, 
                logger,
                TimeSpan.FromSeconds(cacheDurationSeconds));
        });
        services.AddSingleton<IExpiryIndexRepository>(sp => 
            new TableExpiryIndexRepository(expiryIndexTable));
        services.AddSingleton<IUrlIndexRepository>(sp => 
            new TableUrlIndexRepository(urlIndexTable));

        // Services
        services.AddSingleton<IAliasGenerator, AliasGenerator>();
        services.AddSingleton<IUrlValidator, UrlValidator>();
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
    })
    .Build();

host.Run();
