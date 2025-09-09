# Microsoft Greenlight - Source Directory

## MCP Server Configuration

This solution supports Model Context Protocol (MCP) servers for both Claude Code and Visual Studio 2022:

### Quick Setup
When you build the solution in Visual Studio, the VS2022-compatible MCP configuration will be automatically generated from the root `.mcp.json` file.

### Manual Setup
If you need to manually create the VS2022 MCP configuration:

```powershell
# PowerShell (recommended - used by build)
pwsh -ExecutionPolicy Bypass -File setup-vs-mcp.ps1

# Python fallback
python3 setup-vs-mcp.py

# Linux/macOS (requires jq)
./setup-vs-mcp.sh
```

### File Structure
- **Root `/.mcp.json`**: Claude Code compatible format
- **Generated `/src/.mcp.json`**: VS2022 compatible format (auto-generated, do not edit)

### Available MCP Servers
- **ms-docs**: Microsoft documentation search and retrieval
- **fetch**: Web content fetching and processing  
- **playwright**: Browser automation and testing

## Building and Running

See the main [CLAUDE.md](../CLAUDE.md) file for detailed development instructions.