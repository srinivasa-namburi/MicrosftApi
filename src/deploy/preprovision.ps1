# Read the environment and resource group names from environment variables
$envName = $env:AZURE_CONTAINER_REGISTRY_ENDPOINT.Split('.')[0].Substring(3)
$resourceGroup = "rg-"+$env:AZURE_ENV_NAME

$private = Read-Host "Do you want to deploy to a private vnet? (y/[n])"

# Set $private to "n" if the user presses enter
if ($private -eq "") {
    $private = "n"
}

# Delete the infra folder if it exists (use force and no prompting)
Remove-Item -Path infra -Recurse -Force -ErrorAction SilentlyContinue

if ($private -eq "y") {
    Copy-Item -Path infra.private -Destination infra -Recurse

    # Ask for Azure Resource identifier for the private vnet for CAE
    $vnetId = Read-Host "Enter the Azure Resource identifier for the private vnet"
    $env:PVICO_VNET_ID = $vnetId
    
} elseif ($private -eq "n"){
    Copy-Item -Path infra.public -Destination infra -Recurse
} else {
    Write-Host "Invalid choice. Please enter 'y' or 'n', or press enter for the default choice (public deployment)"
    exit
}

# Set the environment variable PVICO_DEPLOYMENT_MODE to private or public depending on the user's choice
if ($private -eq "y") {
    $env:PVICO_DEPLOYMENT_MODE = "private"
} else {
    $env:PVICO_DEPLOYMENT_MODE = "public"
}
