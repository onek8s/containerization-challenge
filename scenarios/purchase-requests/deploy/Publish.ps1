<#
.SYNOPSIS
    Builds the Release configuration and publishes the portal, the API and the jobs to an output folder.
    Requires Visual Studio 2022 or Build Tools for Visual Studio 2022 (MSBuild with the .NET Framework 4.8 targeting pack).
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\publish')
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found. Install Build Tools for Visual Studio 2022.' }

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
$OutputPath = Resolve-Path $OutputPath

& $msbuild "$root\PurchaseRequests.sln" /t:Restore /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

foreach ($site in 'Purchasing.Web', 'Purchasing.Api') {
    & $msbuild "$root\src\$site\$site.csproj" /p:Configuration=Release /p:DeployOnBuild=true /p:DeployDefaultTarget=WebPublish `
        /p:WebPublishMethod=FileSystem /p:DeleteExistingFiles=true /p:PublishUrl="$OutputPath\$site" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Publishing $site failed." }
}

& $msbuild "$root\src\Purchasing.Jobs\Purchasing.Jobs.csproj" /p:Configuration=Release /p:OutDir="$OutputPath\Purchasing.Jobs\" /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Building Purchasing.Jobs failed.' }

Write-Host "Published to $OutputPath"
