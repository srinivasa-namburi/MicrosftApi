# Copyright (c) Microsoft Corporation. All rights reserved.

# Global ARG for runtime base image (MCP execution environment)
# Can be overridden at build time to use custom base image with Python, Node.js, and uv
ARG MCP_BASE_IMAGE=mcr.microsoft.com/dotnet/aspnet:9.0

# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG NUGET_PAT
WORKDIR /src

# Copy solution and all project files for restore
COPY src/*.slnx ./
COPY src/*.sln ./
COPY src/Directory.Build.props ./

# Copy all project files
COPY src/*/*.csproj ./
RUN for file in *.csproj; do \
    dir=$(basename "$file" .csproj); \
    mkdir -p "$dir" && mv "$file" "$dir/"; \
    done

# Copy nested projects
COPY src/Extensibility/*/*.csproj ./Extensibility/
RUN cd Extensibility && \
    for file in *.csproj; do \
    dir=$(basename "$file" .csproj); \
    mkdir -p "$dir" && mv "$file" "$dir/"; \
    done; \
    cd ..

COPY src/Plugins/*/*.csproj ./Plugins/
RUN cd Plugins && \
    for file in *.csproj; do \
    dir=$(basename "$file" .csproj); \
    mkdir -p "$dir" && mv "$file" "$dir/"; \
    done; \
    cd ..

# Copy NuGet config
COPY src/Nuget.ADOArtifacts.config ./nuget.config

# Conditionally add Azure Artifacts source with authentication if NUGET_PAT is provided (internal Microsoft builds)
RUN if [ -n "$NUGET_PAT" ]; then \
        dotnet nuget remove source "PublicPackages" || true; \
        dotnet nuget add source "https://pkgs.dev.azure.com/ipsenergy/industry-permitting/_packaging/industry-permitting_PublicPackages/nuget/v3/index.json" \
        --name "PublicPackages" \
        --username "az" \
        --password "$NUGET_PAT" \
        --store-password-in-clear-text; \
    fi

# Restore dependencies
RUN dotnet restore Microsoft.Greenlight.McpServer.Core/Microsoft.Greenlight.McpServer.Core.csproj

# Copy full source
COPY src/ ./

# Build and publish
WORKDIR /src/Microsoft.Greenlight.McpServer.Core
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime with MCP execution environment
FROM ${MCP_BASE_IMAGE} AS runtime

# Set working directory
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Set entrypoint
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.McpServer.Core.dll"]
