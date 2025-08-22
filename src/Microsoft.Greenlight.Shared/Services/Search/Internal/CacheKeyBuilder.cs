// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text;
namespace Microsoft.Greenlight.Shared.Services.Search.Internal;

internal static class CacheKeyBuilder
{
    public static string Build(string prefix, IEnumerable<KeyValuePair<string, string>> components)
    {
        var ordered = components.OrderBy(k => k.Key, StringComparer.Ordinal);
        var sb = new StringBuilder(prefix);
        foreach (var kv in ordered)
        {
            sb.Append('|').Append(kv.Key).Append('=').Append(kv.Value);
        }
        return sb.ToString();
    }
}
