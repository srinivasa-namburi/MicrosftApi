// Copyright (c) Microsoft. All rights reserved.

namespace ProjectVico.Backend.DocumentIngestion.Shared.Interfaces;

// An interface for processing NRC files
public interface INrcFileProcessor
{
    // A method to download and upload NRC files from a CSV file
    Task ProcessNrcFilesAsync(string csvFileName, string csvContainerName);
}
