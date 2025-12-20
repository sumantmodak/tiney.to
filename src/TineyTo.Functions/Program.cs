using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        // Blob client for GC locking
        var blobServiceClient = new BlobServiceClient(gcBlobConnection);
        var containerClient = blobServiceClient.GetBlobContainerClient(gcBlobContainer);
        containerClient.CreateIfNotExists();
        services.AddSingleton(containerClient);

        // Repositories
        services.AddSingleton<IShortUrlRepository>(sp => 
            new TableShortUrlRepository(shortUrlTable));
        services.AddSingleton<IExpiryIndexRepository>(sp => 
            new TableExpiryIndexRepository(expiryIndexTable));

        // Services
        services.AddSingleton<IAliasGenerator, AliasGenerator>();
        services.AddSingleton<IUrlValidator, UrlValidator>();
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
    })
    .Build();

host.Run();
