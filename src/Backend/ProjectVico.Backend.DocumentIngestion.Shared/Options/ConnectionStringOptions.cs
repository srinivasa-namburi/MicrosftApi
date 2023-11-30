// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Options;

public sealed class ConnectionStringOptions
{
    public const string PropertyName = "ConnectionStrings";
    public string IngestionBlobConnectionString { get; set; } = string.Empty;
}
