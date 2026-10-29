<#
.SYNOPSIS
    Builds and publishes Account Statements into one folder per component: Portal, Api and Distribution.
    Run on the build machine (Visual Studio 2022 or Build Tools with the ASP.NET and web development workload).
.EXAMPLE
    .\Publish.ps1 -OutputPath C:\Build\AccountStatements
#>
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -products * -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild (Visual Studio 2022 or Build Tools) not found.' }

dotnet test (Join-Path $root 'tests\AccountStatements.Tests') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

$targets = [ordered]@{
    'Portal'       = 'src\AccountStatements.Web\AccountStatements.Web.csproj'
    'Api'          = 'src\AccountStatements.Api\AccountStatements.Api.csproj'
    'Distribution' = 'src\AccountStatements.Distribution\AccountStatements.Distribution.csproj'
}

foreach ($name in $targets.Keys) {
    $out = Join-Path $OutputPath $name
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    if ($name -eq 'Distribution') {
        & $msbuild (Join-Path $root $targets[$name]) -restore -t:Publish -p:Configuration=$Configuration -p:PublishDir="$out\" -v:minimal
    }
    else {
        # Web applications: site layout with bin, Views and Web.config
        & $msbuild (Join-Path $root $targets[$name]) -restore -t:WebPublish -p:Configuration=$Configuration -p:WebPublishMethod=FileSystem -p:PublishUrl="$out\" -p:DeleteExistingFiles=true -v:minimal
    }
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed" }
}

Copy-Item (Join-Path $root 'db') (Join-Path $OutputPath 'db') -Recurse -Force
Copy-Item (Join-Path $root 'deploy') (Join-Path $OutputPath 'deploy') -Recurse -Force
Write-Host "Published to $OutputPath"
