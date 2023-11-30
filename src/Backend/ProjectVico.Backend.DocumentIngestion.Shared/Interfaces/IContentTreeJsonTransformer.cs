// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.Backend.DocumentIngestion.Shared.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;

public interface IContentTreeJsonTransformer
{
    Task<List<string>> TransformContentTreeByTitleToConcatenatedJson(List<ContentNode> contentTree);
    

}
