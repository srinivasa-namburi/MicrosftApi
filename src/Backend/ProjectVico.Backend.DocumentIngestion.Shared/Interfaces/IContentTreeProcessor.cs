// Copyright (c) Microsoft. All rights reserved.

using ProjectVico.Backend.DocumentIngestion.Shared.Models;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;

public interface IContentTreeProcessor
{
    void FindSectionHeadings(ContentNode contentNode, List<ContentNode> sectionHeadings);
    Task<int> RemoveReferenceChaptersThroughOpenAiIdentification(List<ContentNode> contentTree);
    int CountContentNodes(ContentNode contentNode);
    ContentNode? FindLastTitleOrHeading(List<ContentNode> contentTree);
}
