using Azure;
using Azure.Data.Tables;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

public class TableUrlIndexRepository : IUrlIndexRepository
{
    private readonly TableClient _tableClient;

    public TableUrlIndexRepository(TableClient tableClient)
    {
        _tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
    }

    public async Task<UrlIndexEntity?> GetByLongUrlAsync(string longUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(longUrl))
            throw new ArgumentException("Long URL cannot be null or empty", nameof(longUrl));
        
        var (partitionKey, rowKey) = UrlIndexEntity.ComputePartitionAndRowKey(longUrl);
        try
        {
            var response = await _tableClient.GetEntityAsync<UrlIndexEntity>(
                partitionKey,
                rowKey,
                cancellationToken: cancellationToken);
            
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> InsertAsync(UrlIndexEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        try
        {
            await _tableClient.AddEntityAsync(entity, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Entity already exists (conflict)
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string longUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(longUrl))
            throw new ArgumentException("Long URL cannot be null or empty", nameof(longUrl));

        var partitionKey = UrlIndexEntity.ComputePartitionKey(longUrl);
        var rowKey = UrlIndexEntity.ComputeRowKey(longUrl);

        try
        {
            await _tableClient.DeleteEntityAsync(
                partitionKey,
                rowKey,
                cancellationToken: cancellationToken);
            
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
