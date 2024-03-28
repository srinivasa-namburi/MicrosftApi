// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Interfaces;

public interface IContentTreeJsonTransformer
{
    Task<List<string>> TransformContentTreeByTitleToConcatenatedJson(List<ContentNode> contentTree);
    

}
