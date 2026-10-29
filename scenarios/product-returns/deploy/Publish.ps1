<#
.SYNOPSIS
    Builds and publishes all Product Returns applications to one folder, one subfolder per application.
.EXAMPLE
    .\Publish.ps1 -Output C:\Build\ProductReturns
#>
param(
    [string]$Output = 'C:\Build\ProductReturns',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

dotnet test (Join-Path $root 'ProductReturns.sln') -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

foreach ($project in 'ProductReturns.Portal', 'ProductReturns.Api', 'ProductReturns.Labels', 'ProductReturns.Jobs') {
    dotnet publish (Join-Path $root "src\$project\$project.csproj") -c $Configuration -r win-x64 --self-contained false -o (Join-Path $Output $project)
    if ($LASTEXITCODE -ne 0) { throw "Publishing $project failed." }
}

Copy-Item (Join-Path $root 'deploy') (Join-Path $Output 'deploy') -Recurse -Force
Write-Host "Published to $Output"
