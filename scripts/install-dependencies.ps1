# Copyright (c) Microsoft Corporation. All rights reserved.
param(
    [switch]$CI,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 Installing Playwright dependencies..." -ForegroundColor Cyan

# Detect platform (use built-in variables when available)
$PlatformWindows = $IsWindows -or $PSVersionTable.Platform -eq "Win32NT" -or $env:OS -eq "Windows_NT"
$PlatformLinux = $IsLinux -or ($PSVersionTable.Platform -eq "Unix" -and (Test-Path "/etc/os-release"))
$PlatformMacOS = $IsMacOS -or ($PSVersionTable.Platform -eq "Unix" -and (Test-Path "/usr/bin/sw_vers"))

Write-Host "   Platform: " -NoNewline
if ($PlatformWindows) { 
    Write-Host "Windows" -ForegroundColor Green
    $Platform = "Windows"
} elseif ($PlatformLinux) { 
    Write-Host "Linux" -ForegroundColor Green
    $Platform = "Linux"
} elseif ($PlatformMacOS) { 
    Write-Host "macOS" -ForegroundColor Green
    $Platform = "macOS"
} else { 
    Write-Host "Unknown" -ForegroundColor Yellow
    $Platform = "Unknown"
}

Write-Host "   CI Mode: $(if ($CI) { 'Yes' } else { 'No' })" -ForegroundColor $(if ($CI) { 'Yellow' } else { 'Gray' })
Write-Host ""

# Check Node.js
try {
    $nodeVersion = node --version 2>$null
    Write-Host "✅ Node.js: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Error "❌ Node.js is required but not installed. Please install Node.js from https://nodejs.org/"
}

# Check npm
try {
    $npmVersion = npm --version 2>$null
    Write-Host "✅ npm: v$npmVersion" -ForegroundColor Green
} catch {
    Write-Error "❌ npm is required but not found."
}

# Install Playwright
Write-Host ""
Write-Host "📦 Installing Playwright..." -ForegroundColor Cyan

try {
    # Install Playwright package if not already installed
    $playwrightInstalled = Test-Path "node_modules/@playwright" 
    if (!$playwrightInstalled -or $Force) {
        Write-Host "   Installing @playwright/test..." -ForegroundColor Gray
        npm install --save-dev @playwright/test
        Write-Host "   ✅ @playwright/test installed" -ForegroundColor Green
    } else {
        Write-Host "   ✅ @playwright/test already installed" -ForegroundColor Green
    }
} catch {
    Write-Error "Failed to install Playwright: $($_.Exception.Message)"
}

# Install browsers based on platform and mode
Write-Host ""
Write-Host "🌐 Installing browsers..." -ForegroundColor Cyan

if ($CI -or $Platform -eq "Linux") {
    # CI or Linux: Install Chromium only for headless testing
    Write-Host "   CI/Linux mode: Installing Chromium only..." -ForegroundColor Yellow
    try {
        npx playwright install chromium
        Write-Host "   ✅ Chromium installed" -ForegroundColor Green
        
        # Install system dependencies on Linux
        if ($Platform -eq "Linux") {
            Write-Host "   Installing system dependencies..." -ForegroundColor Gray
            npx playwright install-deps chromium
            Write-Host "   ✅ System dependencies installed" -ForegroundColor Green
        }
    } catch {
        Write-Warning "Browser installation failed: $($_.Exception.Message)"
    }
} else {
    # Local development: Check for Edge on Windows, minimal browser install
    if ($Platform -eq "Windows") {
        Write-Host "   Windows mode: Checking for Microsoft Edge..." -ForegroundColor Yellow
        
        # Check for Edge in standard locations
        $edgeLocations = @(
            "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
        )
        
        $edgeFound = $false
        foreach ($location in $edgeLocations) {
            if (Test-Path $location) {
                Write-Host "   ✅ Microsoft Edge found at: $location" -ForegroundColor Green
                $edgeFound = $true
                break
            }
        }
        
        if (!$edgeFound) {
            # Try PATH
            $edgePath = Get-Command "msedge" -ErrorAction SilentlyContinue
            if ($edgePath) {
                Write-Host "   ✅ Microsoft Edge found in PATH: $($edgePath.Source)" -ForegroundColor Green
                $edgeFound = $true
            }
        }
        
        if ($edgeFound) {
            Write-Host "   🎯 Using local Microsoft Edge - no browser download needed" -ForegroundColor Green
        } else {
            Write-Host "   Microsoft Edge not found, installing Chromium fallback..." -ForegroundColor Yellow
            npx playwright install chromium
            Write-Host "   ✅ Chromium installed" -ForegroundColor Green
        }
    } else {
        # macOS or other: Install Chromium
        Write-Host "   Installing Chromium..." -ForegroundColor Gray
        npx playwright install chromium
        Write-Host "   ✅ Chromium installed" -ForegroundColor Green
    }
}

# Verify installation
Write-Host ""
Write-Host "🔍 Verifying installation..." -ForegroundColor Cyan

try {
    # Check Playwright CLI
    $playwrightVersion = npx playwright --version 2>$null
    Write-Host "   ✅ Playwright CLI: $playwrightVersion" -ForegroundColor Green
} catch {
    Write-Warning "Playwright CLI verification failed"
}

# Create necessary directories
Write-Host ""
Write-Host "📁 Creating directories..." -ForegroundColor Cyan

$directories = @(
    "playwright/auth",
    "playwright/screenshots", 
    "playwright/test-results"
)

foreach ($dir in $directories) {
    if (!(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "   ✅ Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "   ✅ Exists: $dir" -ForegroundColor Gray
    }
}

# Summary
Write-Host ""
Write-Host "🎉 Installation Summary" -ForegroundColor Green
Write-Host "=" * 30
Write-Host "   Platform: $Platform" -ForegroundColor Gray
Write-Host "   Mode: $(if ($CI) { 'CI/Headless' } else { 'Local Development' })" -ForegroundColor Gray
Write-Host "   Playwright: Installed" -ForegroundColor Green
Write-Host "   Browsers: $(if ($CI -or $Platform -eq 'Linux') { 'Chromium' } else { 'Edge/Chromium' })" -ForegroundColor Green

Write-Host ""
Write-Host "🚀 Ready to run tests!" -ForegroundColor Magenta
Write-Host "   npm test          - Run all tests" -ForegroundColor Gray
Write-Host "   npm run test:headed - Run with visible browser" -ForegroundColor Gray
Write-Host "   npm run browser   - Open application in default Edge profile" -ForegroundColor Gray