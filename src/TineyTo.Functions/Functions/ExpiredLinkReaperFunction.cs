using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Functions;

public class ExpiredLinkReaperFunction
{
    private readonly ILogger<ExpiredLinkReaperFunction> _logger;
    private readonly IShortUrlRepository _shortUrlRepository;
    private readonly IExpiryIndexRepository _expiryIndexRepository;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly ITimeProvider _timeProvider;
    private readonly string _lockBlobName;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);

    public ExpiredLinkReaperFunction(
        ILogger<ExpiredLinkReaperFunction> logger,
        IShortUrlRepository shortUrlRepository,
        IExpiryIndexRepository expiryIndexRepository,
        BlobContainerClient blobContainerClient,
        ITimeProvider timeProvider)
    {
        _logger = logger;
        _shortUrlRepository = shortUrlRepository;
        _expiryIndexRepository = expiryIndexRepository;
        _blobContainerClient = blobContainerClient;
        _timeProvider = timeProvider;
        _lockBlobName = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_BLOB") ?? "expiry-reaper.lock";
    }

    [Function("ExpiredLinkReaper")]
    public async Task Run(
        [TimerTrigger("0 */15 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Expired link reaper starting at: {Time}", _timeProvider.UtcNow);

        // Acquire blob lease for distributed locking
        var blobClient = _blobContainerClient.GetBlobClient(_lockBlobName);
        
        // Ensure the lock blob exists
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            await blobClient.UploadAsync(BinaryData.FromString("lock"), overwrite: true, cancellationToken);
        }

        var leaseClient = blobClient.GetBlobLeaseClient();
        string? leaseId = null;

        try
        {
            var lease = await leaseClient.AcquireAsync(LeaseDuration, cancellationToken: cancellationToken);
            leaseId = lease.Value.LeaseId;
            _logger.LogInformation("Acquired blob lease: {LeaseId}", leaseId);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogInformation("Could not acquire lease, another instance is running. Exiting.");
            return;
        }

        try
        {
            var nowUtc = _timeProvider.UtcNow;
            var todayKey = ExpiryIndexEntity.ComputePartitionKey(nowUtc);
            var yesterdayKey = ExpiryIndexEntity.ComputePartitionKey(nowUtc.AddDays(-1));

            var datePartitions = new[] { yesterdayKey, todayKey };
            var totalDeleted = 0;

            foreach (var datePartition in datePartitions)
            {
                _logger.LogInformation("Scanning expiry partition: {Partition}", datePartition);

                var expiredEntities = await _expiryIndexRepository.GetExpiredAsync(
                    datePartition, 
                    nowUtc, 
                    cancellationToken);

                _logger.LogInformation("Found {Count} expired entries in partition {Partition}", 
                    expiredEntities.Count, datePartition);

                foreach (var indexEntity in expiredEntities)
                {
                    try
                    {
                        // Delete primary entity
                        var primaryDeleted = await _shortUrlRepository.DeleteAsync(
                            indexEntity.AliasRowKey, 
                            cancellationToken);

                        if (primaryDeleted)
                        {
                            _logger.LogDebug("Deleted primary entity for alias: {Alias}", indexEntity.AliasRowKey);
                        }

                        // Delete index entity
                        var indexDeleted = await _expiryIndexRepository.DeleteAsync(
                            indexEntity.PartitionKey, 
                            indexEntity.RowKey, 
                            cancellationToken);

                        if (indexDeleted)
                        {
                            totalDeleted++;
                            _logger.LogDebug("Deleted index entity for alias: {Alias}", indexEntity.AliasRowKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete expired entry for alias: {Alias}", 
                            indexEntity.AliasRowKey);
                        // Continue with next entity - partial progress is safe
                    }
                }
            }

            _logger.LogInformation("Expired link reaper completed. Deleted {Count} entries.", totalDeleted);
        }
        finally
        {
            // Release the lease
            if (leaseId != null)
            {
                try
                {
                    await leaseClient.ReleaseAsync(cancellationToken: cancellationToken);
                    _logger.LogInformation("Released blob lease");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to release blob lease");
                }
            }
        }
    }
}
