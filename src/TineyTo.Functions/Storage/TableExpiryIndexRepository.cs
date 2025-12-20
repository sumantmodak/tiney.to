using Azure;
using Azure.Data.Tables;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

public class TableExpiryIndexRepository : IExpiryIndexRepository
{
    private readonly TableClient _tableClient;

    public TableExpiryIndexRepository(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    public async Task InsertAsync(ExpiryIndexEntity entity, CancellationToken cancellationToken = default)
    {
        await _tableClient.AddEntityAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpiryIndexEntity>> GetExpiredAsync(
        string datePartition, 
        DateTimeOffset beforeUtc, 
        CancellationToken cancellationToken = default)
    {
        var results = new List<ExpiryIndexEntity>();

        // Query all entities in the date partition where ExpiresAtUtc < beforeUtc
        var query = _tableClient.QueryAsync<ExpiryIndexEntity>(
            filter: $"PartitionKey eq '{datePartition}' and ExpiresAtUtc lt datetime'{beforeUtc:O}'",
            cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            results.Add(entity);
        }

        return results;
    }

    public async Task<bool> DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
