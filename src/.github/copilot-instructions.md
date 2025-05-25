// Copyright (c) Microsoft Corporation. All rights reserved.

# Copilot Instructions for Microsoft Greenlight Solution

## General Coding Standards
- Every file must start with:
  // Copyright (c) Microsoft Corporation. All rights reserved.
- Do not use primary constructors except for record classes.
- Use XML documentation (/// <summary>) for all public classes, methods, and properties.
- Use dependency injection for all services, including API clients and backend services.
- Use async/await for all asynchronous operations.

## Feature Implementation Pattern

### Frontend (Blazor Pages & Components)
- UI features are implemented as Blazor pages or components (e.g., `.razor` files).
- Pages and components interact with the backend via API client interfaces (e.g., `IDocumentGenerationApiClient`).
- Use MudBlazor 6 for UI controls and dialogs.
- For real-time updates, use SignalR and subscribe to relevant notifications. Use `ISignalRNotifierGrain` and `SignalRNotifierGrain` for SignalR notifications.
- Do not use backend model classes directly in the frontend. Use Info/DTO classes from the Contracts project.

### API Clients
- API clients (e.g., `DocumentGenerationApiClient`) implement interfaces (e.g., `IDocumentGenerationApiClient`) and are registered in DI in `Program.cs`.
- API clients are responsible for making HTTP requests to backend controllers and returning DTO/Info objects.
- API clients should never expose or use backend model classes.

### Controllers (REST API)
- Controllers (e.g., `DocumentsController`, `McpPluginsController`) are responsible for handling HTTP requests.
- Controllers should only accept and return DTO/Info/Request/Response objects from the Contracts project.
- Controllers use AutoMapper to map between backend models and contract types. These are in the Mappings folder in the `Microsoft.Greenlight.Shared` project.
- For queries (read operations), use REST-style endpoints that return Info/DTO objects.
- For commands (write operations), use REST endpoints that accept Request/Command objects and return Response/Info objects.
- Never expose backend model classes directly in controller responses.
- Using the `DocGenerationDbContext` for database operations is acceptable, but ensure that all data returned to the frontend is in the form of DTO/Info objects.


### Backend Grains (CQRS/Command Handling)
- Command and event-driven features are implemented as Orleans grains (see `Grains` projects in the `30. Orleans Framework` solution folder).
- Grain contracts (interfaces) are defined in `Grains.*.Contracts` projects and implemented in `Grains.*` projects.
- Grains should not expose backend model classes to the outside. Use contract types for all grain method signatures.
- Grains are used for distributed, stateful, or long-running operations (e.g., chat, review, validation, ingestion).
- For any service class that is used by a grain, if accessing entity framework, use `IDbContextFactory<DocGenerationDbContext>` to create a new instance of the `DocGenerationDbContext` for each operation. 
- This ensures that the DbContext is not shared across grains and avoids issues with concurrency and state management.

### SignalR Notifications
- Backend grains and services send notifications via SignalR using notifier grains (e.g., `ISignalRNotifierGrain`).
- The frontend subscribes to SignalR events and updates the UI in real time.
- All notification payloads must use contract types (DTO/Info classes).

## Domain Object Mapping
- Domain objects (e.g., `GeneratedDocument`) are defined in the `Shared.Models` project.
- Mapping profiles (e.g., `GeneratedDocumentProfile`) define how to map between models and contract types (DTO/Info).
- Contract types (e.g., `GeneratedDocumentInfo`) are defined in the `Shared.Contracts` project and are the only types exposed to the frontend or via controllers.
- Never use or expose model classes from `Shared.Models` in the frontend or in controller responses.
- Use request/response objects (e.g., `PromoteContentNodeVersionRequest`) for scenarios where a simple Info/DTO is not sufficient.

## Grains and Contracts Separation
- Grain contracts (interfaces) are always defined in `Grains.*.Contracts` projects.
- Grain implementations are in `Grains.*` projects and reference their contracts.
- Contracts should only use DTO/Info types from the Contracts project, never backend models.
- This separation ensures a clean boundary between distributed logic and the rest of the application.
- The API project only needs to reference the `*.Grains.Contracts` projects, not the `*.Grains` projects. This keeps the API layer clean and focused on contract types.

## Summary
- Always use contract types (DTO/Info/Request/Response) for all API, grain, and notification boundaries.
- Use AutoMapper profiles to map between backend models and contract types.
- Implement features from frontend to backend following the pattern: Page/Component → API Client → Controller → Service/Grain → Model/Database.
- Use SignalR for real-time notifications, always with contract types.
- Never expose backend model classes to the frontend or via API.
- Start every file with the Microsoft copyright notice.
- Avoid primary constructors except for record classes.