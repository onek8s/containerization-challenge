<#
.SYNOPSIS
    Builds and publishes all Document Control components to an output folder:
    Web, Packager, Indexer (win-x64, framework dependent) and Register (desktop COM component).
.EXAMPLE
    .\Publish.ps1 -Output C:\Build\DocControl
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\publish'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

dotnet test (Join-Path $root 'DocumentControl.sln') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

$projects = @{
    Web      = 'src\DocControl.Web\DocControl.Web.csproj'
    Packager = 'src\DocControl.Packager\DocControl.Packager.csproj'
    Indexer  = 'src\DocControl.Indexer\DocControl.Indexer.csproj'
}
foreach ($name in $projects.Keys) {
    dotnet publish (Join-Path $root $projects[$name]) -c $Configuration -r win-x64 --self-contained false -o (Join-Path $Output $name)
    if ($LASTEXITCODE -ne 0) { throw "Publishing $name failed." }
}

dotnet build (Join-Path $root 'src\DocControl.Register\DocControl.Register.csproj') -c $Configuration -o (Join-Path $Output 'Register')
if ($LASTEXITCODE -ne 0) { throw 'Building Register failed.' }

Copy-Item (Join-Path $PSScriptRoot 'excel\TransmittalRegister.bas') (Join-Path $Output 'Register') -Force
Write-Host "Published to $Output"
