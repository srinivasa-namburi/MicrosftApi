#!/usr/bin/env pwsh
param(
    [Parameter()] [string]$ResourceGroup,
    [Parameter()] [string]$ClusterName,
    [Parameter()] [string]$Namespace,
    [Parameter()] [int]$SleepBeforeJobSeconds = 0
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI (az) is required."
}

if (-not $ResourceGroup) { $ResourceGroup = $env:AZURE_RESOURCE_GROUP }
if (-not $ClusterName) { $ClusterName = $env:AKS_CLUSTER_NAME }
if (-not $Namespace) { $Namespace = $env:AKS_NAMESPACE }

if (-not $ResourceGroup) { $ResourceGroup = Read-Host "AKS resource group (cluster RG)" }
if (-not $ClusterName) { $ClusterName = Read-Host "AKS cluster name" }
if (-not $Namespace) { $Namespace = Read-Host "Kubernetes namespace (e.g. greenlight-dev)" }

if (-not $ResourceGroup -or -not $ClusterName -or -not $Namespace) {
    Write-Host "Usage: run-db-setupmanager.ps1 [-ResourceGroup <rg>] [-ClusterName <name>] [-Namespace <ns>] [-SleepBeforeJobSeconds <seconds>]" -ForegroundColor Yellow
    Write-Host "(defaults read from AZURE_RESOURCE_GROUP / AKS_CLUSTER_NAME / AKS_NAMESPACE if set)" -ForegroundColor Yellow
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$runner = Join-Path $scriptDir 'pipeline-internal/build-modern-run-db-setup-job.sh'
if (-not (Test-Path $runner)) {
    Write-Error "Unable to locate pipeline script: $runner"
}

Write-Host "[dbjob-cli] Fetching AKS credentials for $ResourceGroup / $ClusterName"
az aks get-credentials `
  --resource-group $ResourceGroup `
  --name $ClusterName `
  --overwrite-existing `
  --only-show-errors | Out-Null

if ($SleepBeforeJobSeconds -gt 0) {
    Write-Host "[dbjob-cli] Invoking db setup job in namespace '$Namespace' (sleep $SleepBeforeJobSeconds s)"
} else {
    Write-Host "[dbjob-cli] Invoking db setup job in namespace '$Namespace'"
}
& $runner $Namespace $SleepBeforeJobSeconds
