<#
.SYNOPSIS
    Builds and publishes all Order Hub components into one folder per component.
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Build\OrderHub
#>
param(
    [Parameter(Mandatory)] [string] $OutputPath,
    [string] $Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$projects = [ordered]@{
    'Portal'     = 'src\OrderHub.Portal\OrderHub.Portal.csproj'
    'Intake'     = 'src\OrderHub.Intake\OrderHub.Intake.csproj'
    'Fulfilment' = 'src\OrderHub.Fulfilment\OrderHub.Fulfilment.csproj'
    'Partners'   = 'src\OrderHub.Partners\OrderHub.Partners.csproj'
    'ShopSync'   = 'src\OrderHub.ShopSync\OrderHub.ShopSync.csproj'
    'LabTools'   = 'tools\OrderHub.LabTools\OrderHub.LabTools.csproj'
}

dotnet test (Join-Path $root 'tests\OrderHub.Tests\OrderHub.Tests.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

foreach ($name in $projects.Keys) {
    $target = Join-Path $OutputPath $name
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    dotnet publish (Join-Path $root $projects[$name]) -c $Configuration -o $target
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed." }
}

Copy-Item (Join-Path $root 'db') (Join-Path $OutputPath 'db') -Recurse -Force
Copy-Item (Join-Path $root 'reports') (Join-Path $OutputPath 'reports') -Recurse -Force
Copy-Item (Join-Path $root 'samples') (Join-Path $OutputPath 'samples') -Recurse -Force
Write-Host "Published to $OutputPath"
