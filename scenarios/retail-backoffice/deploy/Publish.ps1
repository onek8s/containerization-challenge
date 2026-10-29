#Requires -Version 7.2
<#
.SYNOPSIS
  Builds, tests and publishes every deployable into ..\publish\<Project>.zip.
.DESCRIPTION
  Web applications are framework-dependent (IIS hosting bundle on the hosts). Windows services are
  framework-dependent for win-x64 so that the .exe can be registered with the service control manager.
  The store manager build runs "npm ci" and webpack through the PublishRunWebpack target.
#>
param(
    [string] $Configuration = 'Release',
    [string] $Version = '4.12.0',
    [switch] $SkipTests
)
. "$PSScriptRoot\Common.ps1"

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'publish'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$web = @(
    'VanArsdel.Gateway', 'VanArsdel.BackOffice', 'VanArsdel.StoreManager', 'VanArsdel.Catalog.Api', 'VanArsdel.Pricing.Api',
    'VanArsdel.Allocation.Api', 'VanArsdel.Orders.Api', 'VanArsdel.Customers.Api', 'VanArsdel.Returns.Api',
    'VanArsdel.StoreOps.Api', 'VanArsdel.Reporting.Api'
)
$services = @('VanArsdel.Inventory.Api', 'VanArsdel.ErpConnector', 'VanArsdel.Notifications', 'VanArsdel.PosFeed', 'VanArsdel.Scheduler')

Push-Location $root
try {
    dotnet build RetailBackOffice.sln -c $Configuration -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipTests) {
        dotnet test RetailBackOffice.sln -c $Configuration --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }

    foreach ($project in $web + $services) {
        $target = Join-Path $out $project
        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
        $publishArgs = @("src/$project/$project.csproj", '-c', $Configuration, '-o', $target, "-p:Version=$Version")
        if ($services -contains $project) { $publishArgs += @('-r', 'win-x64', '--self-contained', 'false') }
        Write-Host "Publishing $project" -ForegroundColor Cyan
        dotnet publish @publishArgs
        if ($LASTEXITCODE -ne 0) { throw "Publish of $project failed." }

        # Development settings and local databases never leave the build agent.
        Get-ChildItem $target -Filter 'appsettings.Development.json' | Remove-Item -Force
        Get-ChildItem $target -Filter 'ocelot.Development.json' | Remove-Item -Force -ErrorAction SilentlyContinue

        $zip = "$target.zip"
        if (Test-Path $zip) { Remove-Item $zip -Force }
        Compress-Archive -Path (Join-Path $target '*') -DestinationPath $zip
    }

    # The lab stubs are only published for the test environments.
    dotnet publish src/VanArsdel.LabStubs/VanArsdel.LabStubs.csproj -c $Configuration -o (Join-Path $out 'VanArsdel.LabStubs')
}
finally {
    Pop-Location
}
Write-Host "Packages in $out" -ForegroundColor Green
