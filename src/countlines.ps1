# Function to dynamically load Roslyn from NuGet
function Load-NuGetAssembly {
    param (
        [string]$PackageName,
        [string]$Version = "latest"
    )

    # Create a temporary directory for NuGet packages
    $tempDir = "$env:TEMP\NuGetPackages"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir | Out-Null
    }

    # Use dotnet CLI to install the package
    $nugetPath = "$tempDir\$PackageName"
    dotnet add package $PackageName --version $Version --output $tempDir

    # Load the required DLLs from the package
    $dlls = Get-ChildItem -Path $nugetPath -Recurse -Filter "*.dll"
    foreach ($dll in $dlls) {
        Add-Type -Path $dll.FullName
    }
}

# Load Microsoft.CodeAnalysis.CSharp dynamically
Load-NuGetAssembly -PackageName "Microsoft.CodeAnalysis.CSharp"

function Get-LinesAndMethodCounts {
    param (
        [string]$Directory = (Get-Location).Path
    )

    $totalLines = 0
    $totalPublicMethods = 0
    $totalPrivateMethods = 0

    $files = Get-ChildItem -Path $Directory -Recurse -Include *.cs, *.razor

    foreach ($file in $files) {
        $lines = (Get-Content -Path $file.FullName).Length
        $totalLines += $lines

        if ($file.Extension -eq ".cs") {
            try {
                $code = [System.IO.File]::ReadAllText($file.FullName)
                $syntaxTree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($code)
                $root = $syntaxTree.GetRoot()

                $methods = $root.DescendantNodes() |
                    Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] }

                foreach ($method in $methods) {
                    $modifiers = $method.Modifiers.Text
                    if ($modifiers -contains "public") {
                        $totalPublicMethods++
                    } elseif ($modifiers -contains "private") {
                        $totalPrivateMethods++
                    }
                }
            } catch {
                Write-Warning "Failed to parse file: $($file.FullName)"
            }
        }
    }

    [PSCustomObject]@{
        TotalLines         = $totalLines
        TotalPublicMethods = $totalPublicMethods
        TotalPrivateMethods = $totalPrivateMethods
    }
}

# Run the script and display the result
$result = Get-LinesAndMethodCounts
$result | Format-Table -AutoSize
