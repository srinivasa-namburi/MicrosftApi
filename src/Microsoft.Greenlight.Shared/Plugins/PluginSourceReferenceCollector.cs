using System.Text.Json;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using StackExchange.Redis;

namespace Microsoft.Greenlight.Shared.Plugins;


/// <summary>
/// Collects and manages plugin source reference items in a Redis cache.
/// </summary>
public class PluginSourceReferenceCollector : IPluginSourceReferenceCollector
{
    private readonly IDatabase _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginSourceReferenceCollector"/> class.
    /// </summary>
    /// <param name="redisConnection">The Redis connection multiplexer.</param>
    public PluginSourceReferenceCollector(IConnectionMultiplexer redisConnection)
    {
        _cache = redisConnection.GetDatabase();
    }

    /// <summary>
    /// Adds a plugin source reference item to the Redis cache.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="item">The plugin source reference item.</param>
    public void Add(Guid executionId, PluginSourceReferenceItem item)
    {
        var serializedItem = JsonSerializer.Serialize(item);
        var redisKey = GetRedisKey(executionId);
        _cache.ListRightPush(redisKey, serializedItem);
    }

    /// <summary>
    /// Gets all plugin source reference items from the Redis cache.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <returns>A list of plugin source reference items.</returns>
    public IList<PluginSourceReferenceItem> GetAll(Guid executionId)
    {
        var redisKey = GetRedisKey(executionId);
        var serializedItems = _cache.ListRange(redisKey);
        var items = new List<PluginSourceReferenceItem>();

        foreach (var serializedItem in serializedItems)
        {
            try
            {
                var item = JsonSerializer.Deserialize<PluginSourceReferenceItem>(serializedItem!);
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

    /// <summary>
    /// Clears all plugin source reference items from the Redis cache.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    public void Clear(Guid executionId)
    {
        var redisKey = GetRedisKey(executionId);
        _cache.KeyDelete(redisKey);
    }

    /// <summary>
    /// Gets the Redis key for the specified execution identifier.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <returns>The Redis key.</returns>
    private static string GetRedisKey(Guid executionId)
    {
        return $"PluginSourceReferenceItems:{executionId}";
    }
}
