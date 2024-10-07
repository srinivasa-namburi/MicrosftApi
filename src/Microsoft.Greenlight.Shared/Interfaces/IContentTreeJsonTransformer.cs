// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Interfaces;

public interface IContentTreeJsonTransformer
{
    Task<List<string>> TransformContentTreeByTitleToConcatenatedJson(List<ContentNode> contentTree);
    

}
