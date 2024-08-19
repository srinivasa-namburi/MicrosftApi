<#
.SYNOPSIS
This script creates an Azure AD Application (App Registration) with an associated Service Principal,
sets up API scopes, roles, assigns the executing user to a specific role, and generates a client secret.

.DESCRIPTION
- The script first checks if an Azure AD Application with the specified name already exists.
- If the application already exists, the script outputs instructions on how to delete the existing 
  App Registration using its ID, then exits without making any changes.
- If the application does not exist, the script creates the Azure AD Application and the corresponding 
  Service Principal, sets up API scopes and roles, assigns the executing user to a specific role, and generates a client secret.
- The final output includes the necessary configuration details (Client ID, Client Secret, Tenant ID, etc.) 
  in JSON format, which is intended for use as a GitHub secret or similar secure storage.
- The script also assigns the executing user as the Owner of the App Registration.

.PARAMETER AppName
The display name of the Azure AD Application to create or check for existence. 
If not specified, it defaults to "sp-ms-industrypermitting".

.EXAMPLE
.\sp-create.ps1 -AppName "my-app-registration"

# This command will create an Azure AD Application and Service Principal with the name "my-app-registration".
# If an application with this name already exists, it will provide instructions for deleting the existing application 
# using its ID.

#>

param(
    [string]$AppName = "sp-ms-industrypermitting"
)

# Check if the Azure AD Application already exists
Write-Host "Checking if Azure AD application $AppName already exists..."
$existingApp = az ad app list --filter "displayName eq '$AppName'" | ConvertFrom-Json

if ($existingApp.Count -gt 0) {
    $existingAppId = $existingApp[0].appId
    $existingObjectId = $existingApp[0].id

    Write-Host "Azure AD application $AppName already exists with AppId: $existingAppId and ObjectId: $existingObjectId."

    Write-Host "To delete the existing app registration, use the following command:"
    Write-Host ""
    Write-Host "az ad app delete --id $existingObjectId"
    Write-Host ""
    Write-Host "Exiting script."
    exit
}

# Create Azure AD Application
$tempDir = [System.IO.Path]::GetTempPath()

# Determine the cloud environment
$cloud = az cloud show | ConvertFrom-Json
$cloudName = $cloud.name
$instance = if ($cloudName -eq "AzureUSGovernment") {
    "https://login.microsoftonline.us/"
} else {
    "https://login.microsoftonline.com/"
}

# Get the tenant ID directly from the signed-in user's context
$TenantId = az account show --query 'tenantId' -o tsv

# Get the domain from the tenant
$Domain = az rest --method GET --uri "https://graph.microsoft.com/v1.0/domains" --query "value[0].id" -o tsv

Write-Host "Creating Azure AD application..."
$App = az ad app create `
  --display-name $AppName `
  --enable-access-token-issuance false `
  --enable-id-token-issuance true `
 | ConvertFrom-Json

$AppId = $App.appId
$ObjectId = $App.id

# Create a service principal for the application
$sp = az ad sp create --id $AppId | ConvertFrom-Json
$spObjectId = $sp.id
Write-Host "Service principal created."

# Set the executing user as the owner of the App Registration
$userId = az ad signed-in-user show --query "id" -o tsv
az ad app owner add --id $ObjectId --owner-object-id $userId
Write-Host "Executing user set as Owner of the App Registration."

Write-Host "Setting web redirect URIs..."
$webRedirectUris = @(
    "https://localhost/signin-oidc"
)
az ad app update --id $ObjectId `
    --web-redirect-uris $webRedirectUris
Write-Host "Web redirect URIs set."

Write-Host "Setting application roles..."
$appRoles = @'
[
  {
    "allowedMemberTypes": [
      "User"
    ],
    "description": "Members of this role can generate documents",
    "displayName": "DocumentGeneration",
    "id": "4872d356-5745-421f-b153-0c6a80971173",
    "isEnabled": true,
    "value": "DocumentGeneration"
  }
]
'@
$appRolesFile = Join-Path $tempDir "appRoles.json"
$appRoles | Out-File -FilePath $appRolesFile -Encoding utf8
az ad app update --id $ObjectId --app-roles @$appRolesFile
Write-Host "Application roles set."

Write-Host "Assigning DocumentGeneration role to the executing user..."
$roleAssignmentId = az ad sp show --id $spObjectId | ConvertFrom-Json |
                    Select-Object -ExpandProperty appRoles | 
                    Where-Object { $_.value -eq 'DocumentGeneration' } | 
                    Select-Object -ExpandProperty id

# Create JSON payload for app role assignment
$assignmentPayload = @"
{
    "principalId": "$userId",
    "resourceId": "$spObjectId",
    "appRoleId": "$roleAssignmentId"
}
"@
$assignmentPayloadFile = Join-Path $tempDir "assignmentPayload.json"
$assignmentPayload | Out-File -FilePath $assignmentPayloadFile -Encoding utf8

az rest --method POST --uri "https://graph.microsoft.com/v1.0/users/$userId/appRoleAssignments" `
        --headers "Content-Type=application/json" `
        --body @$assignmentPayloadFile > $null
Write-Host "DocumentGeneration role assigned to the executing user."

Write-Host "Setting Application ID URI..."
$identifierUri = "api://$AppId"
az ad app update --id $ObjectId --identifier-uris "$identifierUri"
Write-Host "Application ID URI set."

Write-Host "Setting API scopes..."
$apiScopeId = [guid]::NewGuid().Guid
$apiScopeJson = @"
{
    "requestedAccessTokenVersion": 2,
    "oauth2PermissionScopes": [
        {
            "adminConsentDescription": "Allows the app to access the web API on behalf of the signed-in user",
            "adminConsentDisplayName": "Access the API on behalf of a user",
            "id": "$apiScopeId",
            "isEnabled": true,
            "type": "User",
            "userConsentDescription": "Allows this app to access the web API on your behalf",
            "userConsentDisplayName": "Access the API on your behalf",
            "value": "access_as_user"
        }
    ]
}
"@
$apiScopesFile = Join-Path $tempDir "apiScopes.json"
$apiScopeJson | Out-File -FilePath $apiScopesFile -Encoding utf8
az ad app update --id $ObjectId --set api=@$apiScopesFile
Write-Host "API scopes set."

Write-Host "Setting required resource access..."
$requiredResourceAccessJson = @"
[
  {
    "resourceAppId": "$AppId",
    "resourceAccess": [
      {
        "id": "$apiScopeId",
        "type": "Scope"
      }
    ]
  }
]
"@
$requiredResourceAccessJson = $requiredResourceAccessJson -replace '\$AppId', $AppId -replace '\$apiScopeId', $apiScopeId
$requiredResourceAccessFile = Join-Path $tempDir "requiredResourceAccess.json"
$requiredResourceAccessJson | Out-File -FilePath $requiredResourceAccessFile -Encoding utf8
az ad app update --id $ObjectId --required-resource-accesses @$requiredResourceAccessFile
Write-Host "Required resource access set."

Write-Host "Generating client secret..."
$clientSecret = az ad app credential reset `
    --id $AppId `
    --append `
    --display-name "Default" `
    --end-date (Get-Date).AddYears(2).ToString("yyyy-MM-dd") `
    | ConvertFrom-Json
$ClientSecret = $clientSecret.password
Write-Host "Client secret generated."

# Cleanup temporary files
Remove-Item $appRolesFile -Force
Remove-Item $apiScopesFile -Force
Remove-Item $requiredResourceAccessFile -Force
Remove-Item $assignmentPayloadFile -Force

Write-Host "Script execution completed."

# Output the relevant settings in the requested format
$outputJson = @{
    Instance = $instance
    Domain = $Domain
    TenantId = $TenantId
    ClientId = $AppId
    ClientSecret = $ClientSecret
    CallbackPath = "/signin-oidc"
    Scopes = "api://$AppId/access_as_user"
} | ConvertTo-Json -Compress

Write-Host "Set the GitHub secret PVICO_ENTRA_CREDENTIALS in your fork of the repo to the following JSON:"
Write-Host "This is not possible to retrieve at a later stage, so please save it now."
Write-Host "The GitHub secret is also not possible to retrieve after you've set it, so take note of it somewhere else as well, if you need to."
Write-Host "***************************************************************************"
Write-Host $outputJson
Write-Host "***************************************************************************"
