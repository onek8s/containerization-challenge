<#
.SYNOPSIS
    Publishes every deployable of Broker Quotes into one folder per application.
.DESCRIPTION
    The quote API and the policy service load RateCore (32-bit COM) in process and are published
    for win-x86. The broker portal publish runs npm ci and npm run build for the React app.
.EXAMPLE
    .\Publish-All.ps1 -Output D:\Drops\BrokerQuotes\5.8.0
#>
param(
    [Parameter(Mandatory)] [string] $Output,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$apps = @(
    @{ Name = 'login';          Project = 'src\Humongous.Identity';        Runtime = $null },
    @{ Name = 'brokers';        Project = 'src\Humongous.BrokerPortal';    Runtime = $null },
    @{ Name = 'workbench';      Project = 'src\Humongous.Workbench';       Runtime = $null },
    @{ Name = 'quote-api';      Project = 'src\Humongous.Quoting.Api';     Runtime = 'win-x86' },
    @{ Name = 'policy-service'; Project = 'src\Humongous.PolicyService';   Runtime = 'win-x86' },
    @{ Name = 'documents';      Project = 'src\Humongous.Documents';       Runtime = $null }
)

foreach ($app in $apps) {
    $target = Join-Path $Output $app.Name
    $runtimeArgs = if ($app.Runtime) { @('-r', $app.Runtime, '--self-contained', 'false') } else { @() }
    Write-Host "Publishing $($app.Name) to $target"
    dotnet publish (Join-Path $root $app.Project) -c $Configuration @runtimeArgs -o $target
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($app.Name) failed." }
}
