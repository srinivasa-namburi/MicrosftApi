
param (
    [string]$Server = "127.0.0.1,9001",
    [string]$Password
)

if (-not $Password) {
    $Password = Read-Host -Prompt "Enter Password" 
}

$ConnectionString = "Server=tcp:$Server;Encrypt=true;TrustServerCertificate=True;Database=ProjectVicoDB;User=sa;Password=$Password"
dotnet ef database update --no-build --project .\Microsoft.Greenlight.Shared\Microsoft.Greenlight.Shared.csproj --connection "$ConnectionString"
