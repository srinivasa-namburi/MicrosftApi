// Copyright (c) Microsoft. All rights reserved.
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Interfaces;

public interface IContentTreeProcessor
{
    void FindSectionHeadings(ContentNode contentNode, List<ContentNode> sectionHeadings);
    int CountContentNodes(ContentNode contentNode);
    ContentNode? FindLastTitleOrHeading(List<ContentNode> contentTree);
}
