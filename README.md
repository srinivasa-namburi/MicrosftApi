## Project Overview

This repo contains the Generative AI for Nuclear Licencing Copilot solution accellerator, intended to reduce the time and cost of creating licencing application documents, and is intended to be built upon and customized to meet users' needs. Any output should always be reviewed by a human.

## Solution Overview

![V2SolutionArchitectureAndDataflow](./docs/assets/V2SolutionArchitectureAndDataflow.png)

## License

Please see [LICENSE](https://github.com/Azure/AI-For-SMRs/blob/main/LICENSE) for this solution's License terms.

## Getting Started with Generative AI

Build a Good Understanding of Generative AI with these courses:

- [Welcoming the generative AI era with Microsoft Azure](https://azure.microsoft.com/en-us/blog/welcoming-the-generative-ai-era-with-microsoft-azure/)
- [Introduction to Generative AI - Training | Microsoft Learn](https://learn.microsoft.com/en-us/training/paths/introduction-generative-ai/)
- [Generative AI with Azure OpenAI Service (DALL-E Overview) - C# Corner](https://www.c-sharpcorner.com/article/generative-ai-with-azure-openai-dall-e-overview/)
- [Develop Generative AI solutions with Azure OpenAI Service Tutorial](https://learn.microsoft.com/en-us/training/paths/develop-ai-solutions-azure-openai/)

## 🛑 STOP - Choose Your Path

Before proceeding, decide what you want to do:

### → **Deploy to Azure Cloud**
- **GitHub Actions (Default)**: Most customers use this. Go to **[DEPLOYMENT.GitHub.md](DEPLOYMENT.GitHub.md)** for complete deployment instructions.
- **Azure DevOps**: Requires copying/syncing repo to ADO. Go to **[DEPLOYMENT.ADO.md](DEPLOYMENT.ADO.md)** for complete deployment instructions.
- **Migrating from Legacy?**: See **[MIGRATION-GUIDE.md](MIGRATION-GUIDE.md)** for upgrade path.

### → **Local Development Only**
Continue reading this guide for setting up your local development environment.

---

## Developer Setup

This section covers **local development setup** for running and testing the application on your machine. We support two main development approaches. **VS Code with Dev Containers is our recommended approach** for consistent cross-platform development.

### 🚀 VS Code + Dev Containers (Recommended)

This approach works identically on **Windows, macOS, and Linux** with a pre-configured development environment.

#### Prerequisites

**🤖 Automated Setup (Optional)**

For convenience, we provide automated setup scripts that install all prerequisites:

- **Windows**: `./scripts/setup-dev-environment.ps1`

  - Installs: Docker Desktop, .NET 9.0 SDK, VS Code, Dev Containers extension
  - Run in PowerShell as Administrator: `powershell -ExecutionPolicy Bypass -File scripts/setup-dev-environment.ps1`

- **macOS/Linux**: `./scripts/setup-dev-environment.sh`
  - Installs: Docker, .NET 9.0 SDK, VS Code, Dev Containers extension
  - Run in terminal: `bash scripts/setup-dev-environment.sh`

**📋 Manual Prerequisites** (if not using automated scripts):

- [Visual Studio Code](https://code.visualstudio.com/) - The modern, cross-platform code editor
- [Docker Desktop](https://docs.docker.com/) - Container runtime
  - **Windows**: Docker Desktop for Windows
  - **macOS**: Docker Desktop for Mac
  - **Linux**: Docker Engine or Docker Desktop for Linux
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers) - VS Code extension for container development

#### Quick Start

1. **Clone and open**:

   ```bash

   code .
   ```

   - VS Code will detect the dev container configuration
   - Click **"Reopen in Container"** when prompted

   - Or use Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`) → "Dev Containers: Reopen in Container"

2. **Container ready**: The container includes everything pre-configured:

   - ✅ Node.js 20 for frontend tooling
   - ✅ PowerShell for scripts

   - ✅ All VS Code extensions (C# Dev Kit, Docker, GitHub Copilot, etc.)

3. **Configure and run** (see [Configuration section](#configuration) below)

#### Dev Container Benefits

- **Consistent environment** - Same setup across Windows, Mac, and Linux
- **No configuration drift** - Everyone gets identical tooling versions
- **Quick onboarding** - New team members can get started quickly
- **Isolated dependencies** - No conflicts with your host machine setup
- **Pre-configured debugging** - F5 debugging works out of the box
- **Automatic port forwarding** - All services accessible from your host browser

### Opening the Project (Dev Container)

You can start the containerized environment using either VS Code or Visual Studio 2022 (Open Folder). Both consume the same `.devcontainer/devcontainer.json`.

#### Option 1: VS Code (Terminal Driven)

1. Clone (if not already):

```bash
git clone <your-fork-url>
cd aismr
```

1. Open VS Code in the repo:

```bash
code .
```

1. When prompted, choose "Reopen in Container" (or `Ctrl+Shift+P` → Dev Containers: Reopen in Container).
1. Wait for first build (images + dotnet workloads). Status bar shows progress.
1. Press `F5` (or Run > Start Debugging) to launch the Aspire AppHost.

#### Option 2: Visual Studio 2022 (Open Folder)

Requires Dev Containers component enabled in the VS Installer.

1. Open Visual Studio 2022.
1. File → Open → Folder… select the repository root.
1. Accept the prompt to open inside a Dev Container.
1. Wait for build; once ready, open `src/Microsoft.Greenlight.slnx` if not auto-loaded.
1. Set `Microsoft.Greenlight.AppHost` as Startup Project (right‑click → Set as Startup Project).
1. Press `F5` to start (Aspire dashboard, web, and API ports will auto-open/forward).

#### Rebuild / Switching Editors

- Rebuild after changing `.devcontainer/devcontainer.json` (VS Code Command Palette or VS Dev Containers menu).
- You can attach the other editor to an already running container; avoid simultaneous rebuilds.
- If prompts don’t appear, manually invoke the reopen command.

### 🏢 Traditional Approach: Visual Studio 2022 (Windows Only)

For developers who prefer the full Visual Studio IDE experience on Windows.

### Automated Docker Setup (Optional)

For convenience, we provide an automated script that installs Docker Desktop:

**📋 Manual Prerequisites** (if not using automated script):

- **Windows 10/11** (Visual Studio 2022 requirement)
- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/) - Community, Professional, or Enterprise
  - Azure development
  - .NET Multi-platform App UI development (optional, for future mobile support)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Git for Windows](https://git-scm.com/download/win) (if not already installed)

#### Setup Steps

1. **Clone the repository**:

   ```bash
   git clone <your-fork-url>
   cd aismr
   ```

2. **Install .NET workloads**:

   ```bash
   dotnet workload install aspire
   dotnet tool install -g dotnet-ef
   ```

3. **Open in Visual Studio**:

   - Launch Visual Studio 2022
   - File → Open → Project/Solution
   - Navigate to `src/Microsoft.Greenlight.sln`

4. **Configure Connected Services** (for Azure resource provisioning):

   - Right-click **Connected Services** in the `Microsoft.Greenlight.AppHost` project
   - Select **Azure Resource Provisioning Settings**
   - Choose your Subscription, Location, and Resource Group for Azure deployments

5. **Configure and run** (see [Configuration section](#configuration) below)

#### Visual Studio Advantages

- **Rich IntelliSense** - Advanced code completion and refactoring
- **Integrated debugging** - Powerful debugger with advanced features
- **Azure integration** - Built-in Azure tools and Connected Services
- **NuGet Package Manager** - GUI for package management
- **Solution Explorer** - Hierarchical project view
- **Built-in Git support** - Team Explorer and Git Changes window

### 🛠 Manual Local Setup (Any OS)

For developers who want full control over their local environment setup:

#### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://docs.docker.com/) or Docker Engine (Linux)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- **Code Editor**: VS Code, Visual Studio 2022, JetBrains Rider, or any preferred editor

#### Install .NET Workloads

```bash
dotnet workload install aspire
dotnet tool install -g dotnet-ef
dotnet dev-certs https --trust  # Trust development certificates
```

#### Editor-Specific Setup

- **VS Code**: Install [C# Dev Kit extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
- **JetBrains Rider**: Built-in .NET support, install Docker plugin
- **Visual Studio 2022**: See Traditional Approach section above

### Prerequisites (All Setups)

Create the following Azure resources:

#### 1. Service Principal for Application Authentication

Run from the repository root:

```powershell
.\build\scripts\sp-create.ps1
```

Take note of the outputted credentials for configuration.

#### 2. Azure Resources

- **Azure OpenAI** (Optional) - Deploy GPT-4o (named "gpt-4o") and text-embedding-ada-002 (named "text-embedding-ada-002")
  - Note: The system can start without OpenAI configured. AI features will be unavailable until configured via the Configuration UI.
- **Azure Maps** - For geospatial functionality

For detailed deployment information, see:

- [Azure DevOps Deployment Guide](./DEPLOYMENT.ADO.md)
- [GitHub Actions Deployment Guide](./DEPLOYMENT.GitHub.md)

### Configuration

All development approaches require the same configuration steps, just with different tools:

#### Step 1: Configure Application Settings

Create `src/Microsoft.Greenlight.AppHost/appsettings.Development.json` with your Azure resource details:

```json
{
  "Azure": {
    "CredentialSource": "AzureCli"
  },
  "AzureAd": {
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "Scopes": "YOUR_SCOPE",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "Instance": "https://login.microsoftonline.com/"
  },
  "ConnectionStrings": {
    "openai-planner": "Endpoint=YOUR_OPENAI_ENDPOINT;Key=YOUR_OPENAI_KEY"
    // Note: openai-planner is optional. The system will start without it, but AI features will be unavailable.
  },
  "ServiceConfiguration": {
    "AzureMaps": {
      "Key": "YOUR_AZURE_MAPS_KEY"
    }
  }
}
```

#### Step 2: Configure User Secrets

**VS Code / Dev Container / Command Line**:

```bash
# Navigate to AppHost directory
cd src/Microsoft.Greenlight.AppHost

# Set user secrets
dotnet user-secrets set "Azure:TenantId" "YOUR_DEPLOYMENT_TENANT_ID"
dotnet user-secrets set "Parameters:sqlPassword" "YOUR_SQL_PASSWORD"
```

**Visual Studio 2022**:

- Right-click on `Microsoft.Greenlight.AppHost` project
- Select **Manage User Secrets**
- Add to the opened `secrets.json`:

```json
{
  "Azure:TenantId": "YOUR_DEPLOYMENT_TENANT_ID",
  "Parameters:sqlPassword": "YOUR_SQL_PASSWORD"
}
```

### Running the Project

#### Step 3: Authenticate with Azure

```bash
az login
```

#### Step 4: Start the Application

**🚀 VS Code + Dev Containers**:

- Press `F5` or use Run → Start Debugging
- Or use Command Palette (`Ctrl+Shift+P`) → "Debug: Start Debugging"
- Or use the integrated terminal: `dotnet run --project src/Microsoft.Greenlight.AppHost`

**🏢 Visual Studio 2022**:

- Press `F5` or click the green "Start" button
- Ensure `Microsoft.Greenlight.AppHost` is set as the startup project

**🛠 Command Line (Any OS)**:

```bash
dotnet run --project src/Microsoft.Greenlight.AppHost
```

#### What Happens When You Run

- **Aspire Dashboard** opens at `https://localhost:17209` - Monitor all services, logs, and metrics
- **Web Application** opens at `https://localhost:5001` - Main user interface
- **API** available at `https://localhost:6001` - Backend services
- **All dependencies** (SQL Server, Redis, etc.) start automatically in containers

## Local Development Credentials (Infrastructure Containers)

The Aspire AppHost starts service containers for local development with fixed, convenience credentials defined in `.devcontainer/devcontainer.json` under `containerEnv`. These values are for **local use only** and must not be reused in any shared or production environment.

| Service                    | Port              | Username         | Password               | Default Database | Connection String (example)                                                                                   |
| -------------------------- | ----------------- | ---------------- | ---------------------- | ---------------- | ------------------------------------------------------------------------------------------------------------- |
| SQL Server                 | 9001              | sa               | StrongP@ssw0rd123!        | ProjectVicoDB    | `Server=localhost,9001;Database=ProjectVicoDB;User Id=sa;Password=StrongP@ssw0rd123!;TrustServerCertificate=true` |
| PostgreSQL (pgvector)      | 9002              | postgres         | DevPgPassword123!        | kmvectordb       | `Host=localhost;Port=9002;Database=kmvectordb;Username=postgres;Password=DevPgPassword123!`                      |
| Redis                      | 16379             | (none)           | (none)                 | n/a              | (no auth in dev)                                                                                              |
| Azurite (Blob/Queue/Table) | 10000/10001/10002 | devstoreaccount1 | (emulator default key) | n/a              | Standard Azurite emulator values                                                                              |

### Overriding Credentials Locally

Preferred: use .NET user secrets so overrides are not committed:

```bash
cd src/Microsoft.Greenlight.AppHost
dotnet user-secrets set "Parameters:sqlPassword" "NewStrongP@ss1"
dotnet user-secrets set "Parameters:postgresPassword" "NewStrongP@ss1"
dotnet user-secrets set "ConnectionStrings:kmvectordb" "Host=localhost;Port=9002;Database=kmvectordb;Username=postgres;Password=NewStrongP@ss1"
```

Restart the AppHost after changing secrets.

You can also temporarily adjust values in `.devcontainer/devcontainer.json` (requires a Dev Container rebuild) but avoid committing sensitive real credentials.

### Vector Store Configuration

#### Local Development
For local development, you can use PostgreSQL with pgvector extension instead of Azure AI Search:

```bash
dotnet user-secrets set "ServiceConfiguration:GreenlightServices:Global:UsePostgresMemory" "true"
```

or setting the environment variable `ServiceConfiguration__GreenlightServices__Global__UsePostgresMemory=true`.

#### Production Deployment
**Important**: Production deployments always use Azure AI Search regardless of the `UsePostgresMemory` setting. The system automatically creates Azure AI Search resources during the Aspire publish process, even if you use PostgreSQL locally. This ensures consistent behavior in production environments.

#### Running Without Vector Store
The system can start without either PostgreSQL or Azure AI Search configured if the OpenAI connection string is not provided. In this mode:
- The application will start successfully
- AI features will be unavailable
- Configuration can be added later via the Configuration UI
- No vector store resources are created until OpenAI is configured

### Security Notes

- These credentials are intentionally weak for frictionless onboarding.
- Do not expose dev ports publicly.
- Production / CI environments must inject secure secrets (see deployment guides) and never rely on the defaults above.

## Troubleshooting

### Common Issues

#### Dev Container Issues

**Problem**: "Failed to connect to Docker"

- **Solution**: Ensure Docker Desktop is running and accessible
- **Linux**: Make sure your user is in the `docker` group: `sudo usermod -aG docker $USER`

**Problem**: "Container failed to start"

- **Solution**: Check Docker Desktop has sufficient resources (8+ CPU cores, 16GB+ RAM recommended)
- Try: "Dev Containers: Rebuild Container" from Command Palette

#### Authentication Issues

**Problem**: "Azure authentication failed"

- **Solution**: Run `az login` and ensure you're authenticated to the correct tenant
- **Check**: `az account show` to verify current subscription

**Problem**: "Service principal not found"

- **Solution**: Re-run `.\build\scripts\sp-create.ps1` to recreate the service principal
- Ensure you have Application Developer permissions in Azure AD

#### Build/Runtime Issues

**Problem**: "Workload 'aspire' not found"

- **Solution**: Install Aspire workload: `dotnet workload install aspire`
- **Update**: `dotnet workload update`

**Problem**: "Port already in use"

- **Solution**:
  - Check what's using the port: `netstat -ano | findstr :5001` (Windows) or `lsof -i :5001` (Mac/Linux)
  - Kill the process or change ports in `src/Microsoft.Greenlight.AppHost/Program.cs`

**Problem**: "SQL Server connection failed"

- **Solution**: Ensure Docker is running and can pull SQL Server container
- **Check**: Container logs in Aspire Dashboard for SQL Server service

#### Performance Issues

**Problem**: "Slow startup on first run"

- **Expected**: First run downloads many container images and NuGet packages
- **Subsequent runs**: Much faster (~30 seconds)

**Problem**: "High memory usage"

- **Expected**: Development setup runs multiple services and containers
- **Minimum**: 16GB RAM recommended, 32GB for optimal experience

### Getting Help

- **Aspire Documentation**: [https://learn.microsoft.com/en-us/dotnet/aspire/](https://learn.microsoft.com/en-us/dotnet/aspire/)
- **Dev Containers Guide**: [https://code.visualstudio.com/docs/devcontainers/containers](https://code.visualstudio.com/docs/devcontainers/containers)
- **Internal Issues**: Check project documentation in `docs/` folder
- **Azure Issues**: Verify subscription access and resource quotas

## Deployment and Customization

For production deployments, see:

- [Azure DevOps Deployment Guide](./DEPLOYMENT.ADO.md)
- [GitHub Actions Deployment Guide](./DEPLOYMENT.GitHub.md)

For customizing and debugging the solution:

- [Exploring, Customizing and Debugging](./docs/deployment/ExploringCustomizingAndDebuggingSolution.md)

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
