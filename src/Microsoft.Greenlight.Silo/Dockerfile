// Copyright (c) Microsoft Corporation. All rights reserved.
# Use the official .NET 9 ASP.NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install dependencies for Python and Node.js
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        gnupg \
        lsb-release \
        software-properties-common \
        python3-pip \
        python3-venv \
        python3-distutils \
        python3.12 \
        python3.12-venv \
        python3.12-distutils \
    && rm -rf /var/lib/apt/lists/*

# Set python3 to python3.12
RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.12 1

# Install uv (Python package manager)
RUN pip3 install --upgrade pip && pip3 install uv

# Install Node.js (latest LTS) and npx
RUN curl -fsSL https://deb.nodesource.com/setup_lts.x | bash - \
    && apt-get update \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# npx is included with nodejs >= 8.2.0

# Set working directory
WORKDIR /app

# Copy published app and set entrypoint (Aspire will handle this in build phase)
# COPY . .
# ENTRYPOINT ["dotnet", "Microsoft.Greenlight.Silo.dll"]
