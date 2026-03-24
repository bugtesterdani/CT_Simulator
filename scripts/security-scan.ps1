param(
    [string]$Solution = "CT3xx.sln"
)

$ErrorActionPreference = "Stop"

Write-Host "Running restore with NuGet audit..."
dotnet restore $Solution

Write-Host "Running vulnerable package scan..."
dotnet list $Solution package --vulnerable --include-transitive

Write-Host "Running outdated package scan..."
dotnet list $Solution package --outdated --include-transitive
