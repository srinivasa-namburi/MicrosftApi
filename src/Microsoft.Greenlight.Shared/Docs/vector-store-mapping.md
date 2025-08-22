// Copyright (c) Microsoft Corporation. All rights reserved.

# Vector Store Mapping Specification

| Legacy Field              | New Mapping                    | Included    | Notes                       |
| ------------------------- | ------------------------------ | ----------- | --------------------------- |
| DocumentId                | Tag: DocumentId                | Yes         | Grouping/citations          |
| FileName                  | Tag: FileName                  | Yes         | Deletes/display             |
| OriginalDocumentUrl       | Tag: OriginalDocumentUrl       | Conditional | When present                |
| PartitionNumber           | Record.PartitionNumber         | Yes         | Adjacency                   |
| IngestedAt                | Record.IngestedAt              | Yes         | Recency weighting potential |
| UserId                    | Tag: UserId, UploadedByUserOid | Conditional | When provided               |
| DocumentLibrary           | Tag: DocumentLibrary           | Yes         | Multi-library segregation   |
| DocumentProcessName       | Tag: DocumentProcessName       | Yes         | Process prompts             |
| IsDocumentLibraryDocument | Tag: IsDocumentLibraryDocument | Yes         | Additional library flag     |
| Custom Tags               | Tag entries                    | Conditional | Passthrough                 |

All tag keys use canonical casing from TagKeys.cs.
