param(
    [string]$AppName = "sp-ms-industrypermitting"
)

# Check if the Azure AD Application already exists
Write-Host "Checking if Azure AD application $AppName already exists..."
$existingApp = az ad app list --display-name $AppName | ConvertFrom-Json

if ($existingApp.Count -gt 0) {
    $existingAppId = $existingApp[0].appId
    $existingObjectId = $existingApp[0].id

    Write-Host "Azure AD application $AppName already exists with AppId: $existingAppId and ObjectId: $existingObjectId."
    Write-Host "To delete the existing app registration, use the following commands:"
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

Write-Host "Creating Azure AD application..."
$App = az ad app create `
  --display-name $AppName `
  --enable-access-token-issuance false `
  --enable-id-token-issuance true `
 | ConvertFrom-Json

$AppId = $App.appId
$ObjectId = $App.id

# Get the domain name
$Domain = (az ad signed-in-user show --query 'userPrincipalName' -o tsv).Split('@')[1]

Write-Host "Azure AD application $AppName created with AppId: $AppId and ObjectId: $ObjectId."

# Update the web section with redirect URIs
$webRedirectUris = @(
    "https://localhost/signin-oidc"
)

Write-Host "Updating web redirect URIs..."
az ad app update --id $ObjectId `
    --web-redirect-uris $webRedirectUris
Write-Host "Web redirect URIs updated."

# Construct the App Roles JSON and write it to a file
Write-Host "Creating App Roles JSON file..."
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
Write-Host "App Roles JSON file created at: $appRolesFile."

# Update App Roles using the JSON file
Write-Host "Updating App Roles..."
az ad app update --id $ObjectId --app-roles @$appRolesFile
Write-Host "App Roles updated."

# Expose an API and set the Application ID URI
$identifierUri = "api://$AppId"
Write-Host "Setting Application ID URI to $identifierUri..."
az ad app update --id $ObjectId --identifier-uris "$identifierUri"
Write-Host "Application ID URI set."

# Add the `access_as_user` scope to the application's API permissions using the string method
Write-Host "Creating API Scopes JSON file..."
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
Write-Host "API Scopes JSON file created at: $apiScopesFile."

# Update API scopes using the JSON file
Write-Host "Updating API scopes..."
az ad app update --id $ObjectId --set api=@$apiScopesFile
Write-Host "API scopes updated."

# Add Required Resource Access for your own API using a temp file
Write-Host "Creating Required Resource Access JSON file..."
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
Write-Host "Required Resource Access JSON file created at: $requiredResourceAccessFile."

# Update the application to include this required resource access
Write-Host "Updating Required Resource Access..."
az ad app update --id $ObjectId --required-resource-accesses @$requiredResourceAccessFile
Write-Host "Required Resource Access updated."

# Generate Client Secret using `az ad app credential reset`
Write-Host "Generating client secret..."
$clientSecret = az ad app credential reset `
    --id $AppId `
    --append `
    --display-name "Default" `
    --end-date (Get-Date).AddYears(2).ToString("yyyy-MM-dd") `
    | ConvertFrom-Json

# Extract the secret from the correct property in the output
$ClientSecret = $clientSecret.password
Write-Host "Client Secret generated."

# Cleanup temporary files
Write-Host "Cleaning up temporary files..."
Remove-Item $appRolesFile -Force
Remove-Item $apiScopesFile -Force
Remove-Item $requiredResourceAccessFile -Force
Write-Host "Temporary files removed. Script execution completed."

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
