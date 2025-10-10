# Copyright (c) Microsoft Corporation. All rights reserved.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG NUGET_PAT
WORKDIR /src
COPY . .

# Conditionally add Azure Artifacts source if NUGET_PAT is provided (internal Microsoft builds)
RUN if [ -n "$NUGET_PAT" ]; then \
        dotnet nuget remove source "PublicPackages" || true; \
        dotnet nuget add source "https://pkgs.dev.azure.com/ipsenergy/industry-permitting/_packaging/industry-permitting_PublicPackages/nuget/v3/index.json" \
        --name "PublicPackages" \
        --username "az" \
        --password "$NUGET_PAT" \
        --store-password-in-clear-text; \
    fi

WORKDIR /src/src/Microsoft.Greenlight.Web.DocGen
RUN dotnet publish -c Release -o /app/publish --os linux --arch x64

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Microsoft.Greenlight.Web.DocGen.dll"]
