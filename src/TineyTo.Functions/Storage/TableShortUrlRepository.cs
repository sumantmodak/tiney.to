using Azure;
using Azure.Data.Tables;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

public class TableShortUrlRepository : IShortUrlRepository
{
    private readonly TableClient _tableClient;

    public TableShortUrlRepository(TableClient tableClient)
    {
        _tableClient = tableClient;
    }

    public async Task<ShortUrlEntity?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var partitionKey = ShortUrlEntity.ComputePartitionKey(alias);
        
        try
        {
            var response = await _tableClient.GetEntityAsync<ShortUrlEntity>(
                partitionKey, 
                alias, 
                cancellationToken: cancellationToken);
            
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> InsertAsync(ShortUrlEntity entity, CancellationToken cancellationToken = default)
    {
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

    public async Task<bool> DeleteAsync(string alias, CancellationToken cancellationToken = default)
    {
        var partitionKey = ShortUrlEntity.ComputePartitionKey(alias);
        
        try
        {
            await _tableClient.DeleteEntityAsync(partitionKey, alias, ETag.All, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
