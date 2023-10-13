<#
.SYNOPSIS
Deploy Project Vico's WebAPI to Azure
#>

param(
    [Parameter(Mandatory)]
    [string]
    # Subscription to which to make the deployment
    $Subscription,

    [Parameter(Mandatory)]
    [string]
    # Resource group to which to make the deployment
    $ResourceGroupName,

    [string]
    # Project Vico WebApi package to deploy
    $PackageFilePath = "$PSScriptRoot/out/webapi.zip"
)

# Ensure $PackageFilePath exists
if (!(Test-Path $PackageFilePath)) {
    Write-Error "Package file '$PackageFilePath' does not exist. Have you run 'package-webapi.ps1' yet?"
    exit 1
}

az account show --output none
if ($LASTEXITCODE -ne 0) {
    Write-Host "Log into your Azure account"
    az login --output none
}

az account set -s $Subscription
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host "Getting Azure WebApp resource name..."
$webAppName=(az resource list --resource-group rg-vico-openai | ConvertFrom-Json | where {$_.type -eq 'Microsoft.Web/sites' -and $_.name -like '*webapi*'} | select name).name
if ($null -eq $webAppName) {
    Write-Error "Could not get Azure WebApp resource name from resource group."
    exit 1
}

Write-Host "Azure WebApp name: $webappName"

Write-Host "Configuring Azure WebApp to run from package..."
az webapp config appsettings set --resource-group $ResourceGroupName --name $webappName --settings WEBSITE_RUN_FROM_PACKAGE="1" | out-null
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Deploying '$PackageFilePath' to Azure WebApp '$webappName'..."
az webapp deployment source config-zip --resource-group $ResourceGroupName --name $webappName --src $PackageFilePath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}