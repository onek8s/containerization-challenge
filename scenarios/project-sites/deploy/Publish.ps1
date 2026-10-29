<#
.SYNOPSIS
    Builds the solution on the build server and writes a versioned drop:
    farm\   Lucerne.ProjectSites.wsp, Lucerne.ProjectSites.Claims.wsp
    addin\  published add-in web site + Lucerne.ProjectSites.Dashboards.app
    sync\   sync service binaries
    admin\  psadmin.exe
    db\     SQL scripts
.EXAMPLE
    .\Publish.ps1 -Version 5.3.1
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $Configuration = 'Release',
    [string] $MSBuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
$settings = Get-ProjectSitesSettings
$root = Split-Path -Parent $PSScriptRoot
$drop = Join-Path $settings.DropRoot $Version
if (Test-Path $drop) { throw "Drop $drop already exists." }

Write-Step "Restoring and building $Configuration with $MSBuild"
& $MSBuild (Join-Path $root 'ProjectSites.sln') /restore /m /nologo /v:minimal /p:Configuration=$Configuration /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$staging = Join-Path $env:TEMP "ProjectSites-$Version"
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $staging | Out-Null

Write-Step 'Packaging farm solutions'
$farm = New-Item -ItemType Directory -Path (Join-Path $staging 'farm')
& (Join-Path $PSScriptRoot 'New-SolutionPackage.ps1') -Project 'Lucerne.ProjectSites.Farm' -Configuration $Configuration -OutputPath $farm.FullName
& (Join-Path $PSScriptRoot 'New-SolutionPackage.ps1') -Project 'Lucerne.ProjectSites.Claims' -Configuration $Configuration -OutputPath $farm.FullName

Write-Step 'Publishing the add-in web site'
$addin = Join-Path $staging 'addin'
& $MSBuild (Join-Path $root 'src\Lucerne.ProjectSites.Dashboards\Lucerne.ProjectSites.Dashboards.csproj') /nologo /v:minimal `
    /p:Configuration=$Configuration /p:DeployOnBuild=true /p:WebPublishMethod=FileSystem /p:PublishUrl="$addin\site" /p:DeleteExistingFiles=true
if ($LASTEXITCODE -ne 0) { throw 'Publishing the add-in failed.' }
& (Join-Path $root 'addin\New-AppPackage.ps1') -OutputPath $addin -Version $Version

Write-Step 'Copying the sync service and admin tool'
Copy-Item (Join-Path $root "src\Lucerne.ProjectSites.SyncService\bin\$Configuration\net472") (Join-Path $staging 'sync') -Recurse
Copy-Item (Join-Path $root "src\Lucerne.ProjectSites.AdminTool\bin\$Configuration\net472") (Join-Path $staging 'admin') -Recurse
Copy-Item (Join-Path $root 'db') (Join-Path $staging 'db') -Recurse

Write-Step "Copying to $drop"
Copy-Item $staging $drop -Recurse
Write-Step 'Done'
