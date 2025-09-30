# Copyright (c) Microsoft Corporation. All rights reserved.
# Visual Studio 2022 Development Environment Setup Script
# Installs: Docker Desktop only (for VS2022 development)

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Microsoft Greenlight - Visual Studio 2022 Environment Setup" -ForegroundColor Magenta
Write-Host "=" * 65
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Warning "⚠️  This script should be run as Administrator for optimal experience"
    Write-Host "   Docker installation may require elevated privileges" -ForegroundColor Yellow
    Write-Host ""
}

# Check if winget is available
try {
    $wingetVersion = winget --version 2>$null
    Write-Host "✅ Windows Package Manager: $wingetVersion" -ForegroundColor Green
} catch {
    Write-Error "❌ Windows Package Manager (winget) is required but not found. Please install from Microsoft Store or GitHub."
    exit 1
}

Write-Host ""

# Function to check if a command exists
function Test-Command {
    param($Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    } catch {
        return $false
    }
}

# Check Visual Studio 2022
Write-Host "🔍 Visual Studio 2022 Detection" -ForegroundColor Cyan
Write-Host "-" * 35

$vsInstallations = @()
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

if (Test-Path $vswhere) {
    try {
        $vsInstallations = & $vswhere -version "[17.0,18.0)" -property displayName,installationPath -format json | ConvertFrom-Json
    } catch {
        Write-Warning "   ⚠️  Could not query Visual Studio installations"
    }
}

if ($vsInstallations.Count -gt 0) {
    foreach ($vs in $vsInstallations) {
        Write-Host "   ✅ Found: $($vs.displayName)" -ForegroundColor Green
        Write-Host "      Path: $($vs.installationPath)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "   🎯 Visual Studio 2022 detected - proceeding with Docker setup" -ForegroundColor Green
} else {
    Write-Host "   ❌ Visual Studio 2022 not found" -ForegroundColor Red
    Write-Host ""
    Write-Host "   📋 This script is designed for Visual Studio 2022 development." -ForegroundColor Yellow
    Write-Host "   Please install Visual Studio 2022 with the following workloads:" -ForegroundColor Yellow
    Write-Host "      • ASP.NET and web development" -ForegroundColor Gray
    Write-Host "      • .NET desktop development" -ForegroundColor Gray
    Write-Host "      • Azure development" -ForegroundColor Gray
    Write-Host ""
    
    $continue = Read-Host "Continue with Docker installation anyway? (y/N)"
    if ($continue -ne 'y' -and $continue -ne 'Y') {
        Write-Host "   Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""

# Docker Desktop Installation
Write-Host "🐳 Docker Desktop" -ForegroundColor Cyan
Write-Host "-" * 20

if (Test-Command "docker" -and -not $Force) {
    try {
        $dockerVersion = docker --version 2>$null
        Write-Host "   ✅ Already installed: $dockerVersion" -ForegroundColor Green
        
        # Check if Docker Desktop is running
        try {
            docker info 2>$null | Out-Null
            Write-Host "   ✅ Docker Desktop is running" -ForegroundColor Green
        } catch {
            Write-Host "   ⚠️  Docker Desktop is installed but not running" -ForegroundColor Yellow
            Write-Host "      Please start Docker Desktop from the Start menu" -ForegroundColor Gray
        }
    } catch {
        Write-Host "   ⚠️  Docker command found but not responding - may need restart" -ForegroundColor Yellow
    }
} else {
    Write-Host "   📦 Installing Docker Desktop..." -ForegroundColor Yellow
    try {
        winget install --id Docker.DockerDesktop --accept-source-agreements --accept-package-agreements
        Write-Host "   ✅ Docker Desktop installed" -ForegroundColor Green
        Write-Host "   ⚠️  Please restart your computer and start Docker Desktop" -ForegroundColor Yellow
    } catch {
        Write-Warning "   ❌ Failed to install Docker Desktop: $($_.Exception.Message)"
        Write-Host "      You can manually download Docker Desktop from: https://www.docker.com/products/docker-desktop" -ForegroundColor Gray
    }
}

Write-Host ""

# Docker Configuration Check
Write-Host "🔧 Docker Configuration" -ForegroundColor Cyan
Write-Host "-" * 25

Write-Host "   📋 Recommended Docker Desktop settings for Visual Studio 2022:" -ForegroundColor Yellow
Write-Host "      • Memory: At least 4GB (8GB+ recommended)" -ForegroundColor Gray
Write-Host "      • CPUs: At least 2 cores (4+ recommended)" -ForegroundColor Gray
Write-Host "      • Enable WSL 2 integration (if using WSL)" -ForegroundColor Gray
Write-Host "      • Enable Kubernetes (optional)" -ForegroundColor Gray

Write-Host ""

# Visual Studio Integration
Write-Host "🔗 Visual Studio Integration" -ForegroundColor Cyan
Write-Host "-" * 30

Write-Host "   📋 To use Docker with Visual Studio 2022:" -ForegroundColor Yellow
Write-Host "      1. Ensure Docker Desktop is running" -ForegroundColor Gray
Write-Host "      2. Open your project in Visual Studio 2022" -ForegroundColor Gray
Write-Host "      3. Right-click project → Add → Container Orchestrator Support" -ForegroundColor Gray
Write-Host "      4. Or right-click project → Add → Docker Support" -ForegroundColor Gray

Write-Host ""

# Summary
Write-Host "🎉 Installation Summary" -ForegroundColor Green
Write-Host "=" * 30
Write-Host "   ✅ Docker Desktop" -ForegroundColor Green

Write-Host ""
Write-Host "🚀 Next Steps for Visual Studio 2022:" -ForegroundColor Magenta
Write-Host "   1. Start Docker Desktop (if not already running)" -ForegroundColor Gray
Write-Host "   2. Open your project in Visual Studio 2022" -ForegroundColor Gray
Write-Host "   3. Enable Docker support in your project" -ForegroundColor Gray
Write-Host "   4. Use F5 to run with Docker debugging" -ForegroundColor Gray
Write-Host ""
Write-Host "📚 For container development with Visual Studio, see:" -ForegroundColor Cyan
Write-Host "   https://docs.microsoft.com/en-us/visualstudio/containers/" -ForegroundColor Gray