<#
.SYNOPSIS
    Builds the Release configuration and publishes the portal, the service, the intake service and the jobs to an output folder.
    Requires Visual Studio 2022 or Build Tools for Visual Studio 2022 (MSBuild with the .NET Framework 4.8 targeting pack and web build tools).
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

& $msbuild "$root\PropertyInspections.sln" /t:Restore /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

foreach ($site in 'Inspections.Web', 'Inspections.Service') {
    & $msbuild "$root\src\$site\$site.csproj" /p:Configuration=Release /p:DeployOnBuild=true /p:DeployDefaultTarget=WebPublish `
        /p:WebPublishMethod=FileSystem /p:DeleteExistingFiles=true /p:PublishUrl="$OutputPath\$site" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Publishing $site failed." }
}

foreach ($exe in 'Inspections.Jobs', 'Inspections.Intake') {
    & $msbuild "$root\src\$exe\$exe.csproj" /p:Configuration=Release /p:OutDir="$OutputPath\$exe\" /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Building $exe failed." }
}

Write-Host "Published to $OutputPath"
