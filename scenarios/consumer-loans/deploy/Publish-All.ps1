<#
.SYNOPSIS
  Publishes all Consumer Loans deployables to .\out (Release, framework dependent, win-x64).
  Needs the .NET SDK (6.0.4xx or newer) and Node.js 18+ for the customer portal client.
#>
param(
    [string] $Output = (Join-Path $PSScriptRoot '..\out'),
    [switch] $IncludeLabStubs
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$projects = [ordered]@{
    'apply'          = 'src\Lucerne.Lending.CustomerPortal\Lucerne.Lending.CustomerPortal.csproj'
    'lending-api'    = 'src\Lucerne.Lending.Api\Lucerne.Lending.Api.csproj'
    'lending-office' = 'src\Lucerne.Lending.BackOffice\Lucerne.Lending.BackOffice.csproj'
    'documents'      = 'src\Lucerne.Lending.Documents\Lucerne.Lending.Documents.csproj'
    'collections'    = 'src\Lucerne.Lending.Collections\Lucerne.Lending.Collections.csproj'
    'loantape'       = 'src\Lucerne.Lending.LoanTape\Lucerne.Lending.LoanTape.csproj'
}
if ($IncludeLabStubs) { $projects['lending-lab'] = 'tools\Lucerne.Lending.LabStubs\Lucerne.Lending.LabStubs.csproj' }

dotnet test (Join-Path $root 'ConsumerLoans.sln') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

foreach ($name in $projects.Keys) {
    $target = Join-Path $Output $name
    Write-Host "==> $name -> $target" -ForegroundColor Cyan
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    dotnet publish (Join-Path $root $projects[$name]) -c Release -r win-x64 --self-contained false -o $target
    if ($LASTEXITCODE -ne 0) { throw "Publish of $name failed." }
}
