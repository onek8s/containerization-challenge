<#
.SYNOPSIS
    Builds and publishes all Control Tower applications into one output folder.
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Build\ControlTower
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests
)

. "$PSScriptRoot\common.ps1"

$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'ControlTower.sln'
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

foreach ($tool in 'dotnet', 'node', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool was not found on PATH." }
}

Write-Step 'Restore and build'
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if (-not $SkipTests) {
    Write-Step 'Tests'
    & dotnet test (Join-Path $root 'tests\ControlTower.Tests') -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}

$projects = @(
    'src\ControlTower.Identity',
    'src\ControlTower.Booking.Web',
    'src\ControlTower.Portal',
    'src\ControlTower.Tower.Api',
    'src\ControlTower.Scanning.Api',
    'src\ControlTower.CarrierGateway',
    'src\ControlTower.Telematics',
    'src\ControlTower.Events',
    'src\ControlTower.Jobs',
    'tools\ControlTower.LabTools'
)

foreach ($project in $projects) {
    $name = Split-Path $project -Leaf
    Write-Step "Publish $name"
    # The operations SPA (Tower.Api\ClientApp) is built by the project's publish target with npm.
    & dotnet publish (Join-Path $root $project) -c $Configuration -r $Runtime --self-contained false -o (Join-Path $OutputPath $name)
    if ($LASTEXITCODE -ne 0) { throw "Publish of $name failed." }
}

Copy-Item (Join-Path $root 'db') (Join-Path $OutputPath 'db') -Recurse -Force
Copy-Item (Join-Path $root 'deploy') (Join-Path $OutputPath 'deploy') -Recurse -Force

$commit = (& git -C $root rev-parse --short HEAD 2>$null)
@{ built = (Get-Date).ToString('s'); configuration = $Configuration; commit = $commit } |
    ConvertTo-Json | Set-Content (Join-Path $OutputPath 'build.json')

Write-Host ''
Write-Host "Published to $OutputPath"
