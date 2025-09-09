# Copyright (c) Microsoft Corporation. All rights reserved.
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("start", "stop", "restart", "status")]
    [string]$Action,
    
    [switch]$Watch
)

# Aspire ports to monitor (dashboard + forwarded app ports)
$AspirePorts = @(17209, 5001, 6001, 6002)
$ProcessName = "Microsoft.Greenlight.AppHost"

function Get-AspireProcesses {
    return Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -and ($_.CommandLine.Contains("Microsoft.Greenlight.AppHost") -or $_.CommandLine.Contains("Aspire"))
    }
}

function Get-PortProcesses {
    $processes = @()
    foreach ($port in $AspirePorts) {
        try {
            $netstat = netstat -ano | Select-String ":$port.*LISTENING"
            if ($netstat) {
                foreach ($line in $netstat) {
                    $pid = ($line -split '\s+')[-1]
                    if ($pid -match '^\d+$') {
                        $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
                        if ($process) {
                            $processes += [PSCustomObject]@{
                                Port = $port
                                PID = $pid
                                ProcessName = $process.ProcessName
                            }
                        }
                    }
                }
            }
        } catch {
            Write-Warning "Could not check port $port"
        }
    }
    return $processes
}

function Stop-Solution {
    Write-Host "🛑 Stopping Greenlight solution..." -ForegroundColor Yellow
    
    # Find Aspire processes
    $aspireProcs = Get-AspireProcesses
    if ($aspireProcs) {
        Write-Host "   Found $($aspireProcs.Count) Aspire processes" -ForegroundColor Cyan
        foreach ($proc in $aspireProcs) {
            Write-Host "   Stopping PID $($proc.Id)..." -ForegroundColor Gray
            try {
                Stop-Process -Id $proc.Id -Force
                Write-Host "   ✅ Stopped PID $($proc.Id)" -ForegroundColor Green
            } catch {
                Write-Warning "   Could not stop PID $($proc.Id): $($_.Exception.Message)"
            }
        }
    }
    
    # Find processes using our ports
    $portProcs = Get-PortProcesses
    if ($portProcs) {
        Write-Host "   Found $($portProcs.Count) processes using Aspire ports" -ForegroundColor Cyan
        foreach ($proc in $portProcs) {
            Write-Host "   Stopping PID $($proc.PID) on port $($proc.Port)..." -ForegroundColor Gray
            try {
                Stop-Process -Id $proc.PID -Force
                Write-Host "   ✅ Stopped PID $($proc.PID)" -ForegroundColor Green
            } catch {
                Write-Warning "   Could not stop PID $($proc.PID): $($_.Exception.Message)"
            }
        }
    }
    
    # Wait a moment for cleanup
    Start-Sleep -Seconds 2
    Write-Host "✅ Solution stopped" -ForegroundColor Green
}

function Test-SolutionRunning {
    # Check for processes
    $aspireProcs = Get-AspireProcesses
    $portProcs = Get-PortProcesses
    
    if ($aspireProcs -or $portProcs) {
        return $true
    }
    
    # Test HTTP connectivity  
    foreach ($port in $AspirePorts) {
        try {
            $response = Invoke-WebRequest -Uri "https://localhost:$port" -SkipCertificateCheck -TimeoutSec 5 -ErrorAction SilentlyContinue
            if ($response.StatusCode -lt 500) {
                return $true
            }
        } catch {
            # Port not responding, continue checking
        }
    }
    
    return $false
}

function Start-Solution {
    Write-Host "🚀 Starting Greenlight solution..." -ForegroundColor Green
    
    # Ensure we're in the right directory
    $srcPath = Join-Path $PSScriptRoot "..\src"
    if (!(Test-Path $srcPath)) {
        throw "Source directory not found: $srcPath"
    }
    
    # Check Docker is running (required for Aspire dependencies)
    try {
        docker info 2>&1 | Out-Null
        Write-Host "   ✅ Docker is running" -ForegroundColor Green
    } catch {
        Write-Warning "   Docker may not be running. Aspire requires Docker for dependencies."
    }
    
    Push-Location $srcPath
    try {
        $command = if ($Watch) { 
            "dotnet watch --project Microsoft.Greenlight.AppHost" 
        } else { 
            "dotnet run --project Microsoft.Greenlight.AppHost" 
        }
        
        Write-Host "   Running: $command" -ForegroundColor Cyan
        Write-Host "   Directory: $(Get-Location)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "🎯 Aspire dashboard will be available at: https://localhost:17209" -ForegroundColor Magenta
        Write-Host "🎯 Main application will be available at: https://localhost:7243" -ForegroundColor Magenta
        Write-Host ""
        Write-Host "Press Ctrl+C to stop the solution" -ForegroundColor Yellow
        Write-Host ""
        
        # Start the solution
        Invoke-Expression $command
    } finally {
        Pop-Location
    }
}

function Show-Status {
    Write-Host "📊 Greenlight Solution Status" -ForegroundColor Cyan
    Write-Host "=" * 40
    
    $aspireProcs = Get-AspireProcesses
    $portProcs = Get-PortProcesses
    
    if ($aspireProcs) {
        Write-Host "🟢 Aspire Processes:" -ForegroundColor Green
        foreach ($proc in $aspireProcs) {
            Write-Host "   PID $($proc.Id): $($proc.ProcessName)" -ForegroundColor Gray
        }
    } else {
        Write-Host "🔴 No Aspire processes found" -ForegroundColor Red
    }
    
    Write-Host ""
    
    if ($portProcs) {
        Write-Host "🟢 Port Usage:" -ForegroundColor Green
        foreach ($proc in $portProcs) {
            Write-Host "   Port $($proc.Port): PID $($proc.PID) ($($proc.ProcessName))" -ForegroundColor Gray
        }
    } else {
        Write-Host "🔴 No processes using Aspire ports" -ForegroundColor Red
    }
    
    Write-Host ""
    
    # Test HTTP connectivity
    Write-Host "🌐 HTTP Connectivity:" -ForegroundColor Cyan
    $endpoints = @(
        @{ Name = "Aspire Dashboard"; Url = "https://localhost:17209" },
        @{ Name = "Web DocGen (Frontend)"; Url = "https://localhost:5001" },
        @{ Name = "API Main"; Url = "https://localhost:6001" },
        @{ Name = "MCP Server"; Url = "https://localhost:6002" }
    )
    
    foreach ($endpoint in $endpoints) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint.Url -SkipCertificateCheck -TimeoutSec 5 -ErrorAction SilentlyContinue
            Write-Host "   ✅ $($endpoint.Name): $($response.StatusCode)" -ForegroundColor Green
        } catch {
            Write-Host "   ❌ $($endpoint.Name): Not responding" -ForegroundColor Red
        }
    }
    
    $isRunning = Test-SolutionRunning
    Write-Host ""
    Write-Host "Overall Status: " -NoNewline
    if ($isRunning) {
        Write-Host "🟢 RUNNING" -ForegroundColor Green
    } else {
        Write-Host "🔴 STOPPED" -ForegroundColor Red
    }
}

# Main execution
switch ($Action) {
    "start" {
        if (Test-SolutionRunning) {
            Write-Host "⚠️  Solution appears to be already running" -ForegroundColor Yellow
            Show-Status
            $response = Read-Host "Do you want to restart it? (y/N)"
            if ($response -eq 'y' -or $response -eq 'Y') {
                Stop-Solution
                Start-Sleep -Seconds 3
                Start-Solution
            }
        } else {
            Start-Solution
        }
    }
    "stop" {
        if (Test-SolutionRunning) {
            Stop-Solution
        } else {
            Write-Host "✅ Solution is not running" -ForegroundColor Green
        }
    }
    "restart" {
        if (Test-SolutionRunning) {
            Stop-Solution
            Start-Sleep -Seconds 3
        }
        Start-Solution
    }
    "status" {
        Show-Status
    }
}