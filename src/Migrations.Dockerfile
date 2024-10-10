FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
COPY . .

# Build the project in the default output directory
# RUN dotnet workload install aspire
# RUN dotnet build "Microsoft.Greenlight.sln" -c Release
RUN dotnet build "Microsoft.Greenlight.Shared/Microsoft.Greenlight.Shared.csproj"

# Install the EF Core CLI tools globally
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Use shell form of ENTRYPOINT to allow for variable expansion
# ENTRYPOINT dotnet ef database update --no-build --startup-project /app/Microsoft.Greenlight.AppHost/Microsoft.Greenlight.AppHost.csproj --project /app/Microsoft.Greenlight.Shared/Microsoft.Greenlight.Shared.csproj --connection "Server=tcp:${SQL_SERVER_FQDN},1433;Encrypt=True;Database=${DB_NAME};Authentication=Active Directory Default"
ENTRYPOINT dotnet ef database update --no-build --project /app/Microsoft.Greenlight.Shared/Microsoft.Greenlight.Shared.csproj --connection "Server=tcp:${SQL_SERVER_FQDN},1433;Encrypt=True;Database=${DB_NAME};Authentication=Active Directory Default"