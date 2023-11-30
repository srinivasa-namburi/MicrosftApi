// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace ProjectVico.Backend.DocumentIngestion.Shared.Models;

public class ContentNode
{
    public ContentNode()
    {
        this.Id = Guid.NewGuid();
    }

    public ContentNode(Guid idGuid)
    {
        this.Id = idGuid;
    }

    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public ContentNodeType Type { get; set; }
    public List<ContentNode> Children { get; set; } = new List<ContentNode>();

    [JsonIgnore]
    public ContentNode? Parent { get; set; }
}

