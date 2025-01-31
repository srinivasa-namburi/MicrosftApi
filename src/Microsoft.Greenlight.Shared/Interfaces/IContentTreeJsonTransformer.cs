// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Interfaces;

/// <summary>
/// Interface for transforming a content tree into concatenated JSON strings.
/// </summary>
public interface IContentTreeJsonTransformer
{
    /// <summary>
    /// Transforms a content tree by title to concatenated JSON strings.
    /// </summary>
    /// <param name="contentTree">The content tree to transform.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of concatenated JSON strings.</returns>
    Task<List<string>> TransformContentTreeByTitleToConcatenatedJson(List<ContentNode> contentTree);
}
