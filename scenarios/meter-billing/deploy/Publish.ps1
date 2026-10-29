<#
.SYNOPSIS
    Builds and publishes the Meter Billing components into one folder per component:
    Engine, Monitor and Lookup. Run on the build machine.
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Build\MeterBilling
#>
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

dotnet test (Join-Path $root 'tests\MeterBilling.Core.Tests') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

$targets = [ordered]@{
    'Engine'  = 'src\MeterBilling.Engine\MeterBilling.Engine.csproj'
    'Monitor' = 'src\MeterBilling.Monitor\MeterBilling.Monitor.csproj'
    'Lookup'  = 'src\MeterBilling.Lookup\MeterBilling.Lookup.csproj'
}

foreach ($name in $targets.Keys) {
    $out = Join-Path $OutputPath $name
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish (Join-Path $root $targets[$name]) -c $Configuration -o $out
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed" }
}

Copy-Item (Join-Path $root 'db') (Join-Path $OutputPath 'db') -Recurse -Force
Copy-Item (Join-Path $root 'deploy') (Join-Path $OutputPath 'deploy') -Recurse -Force
Write-Host "Published to $OutputPath"
