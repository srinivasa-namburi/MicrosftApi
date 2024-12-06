using System.Text.Json;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Plugins;


public class PluginSourceReferenceCollector : IPluginSourceReferenceCollector
{
    private readonly IDatabase _cache;

    public PluginSourceReferenceCollector(IConnectionMultiplexer redisConnection)
    {
        _cache = redisConnection.GetDatabase();
    }

    public void Add(Guid executionId, PluginSourceReferenceItem item)
    {
        var serializedItem = JsonSerializer.Serialize(item);
        var redisKey = GetRedisKey(executionId);
        _cache.ListRightPush(redisKey, serializedItem);
    }

    public IList<PluginSourceReferenceItem> GetAll(Guid executionId)
    {
        var redisKey = GetRedisKey(executionId);
        var serializedItems = _cache.ListRange(redisKey);
        var items = new List<PluginSourceReferenceItem>();

        foreach (var serializedItem in serializedItems)
        {
            try
            {
                var item = JsonSerializer.Deserialize<PluginSourceReferenceItem>(serializedItem);
                if (item != null)
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // Optionally log the error
                // Continue to the next item
            }
        }

        return items;
    }

    public void Clear(Guid executionId)
    {
        var redisKey = GetRedisKey(executionId);
        _cache.KeyDelete(redisKey);
    }

    private static string GetRedisKey(Guid executionId)
    {
        return $"PluginSourceReferenceItems:{executionId}";
    }
}
