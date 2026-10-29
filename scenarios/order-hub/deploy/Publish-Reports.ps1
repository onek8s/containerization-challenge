<#
.SYNOPSIS
    Publishes the invoice report to the report server (SQL Server Reporting Services 2017 or later, REST API v2.0).
.DESCRIPTION
    Creates the folder "/Order Hub", the shared data source "OrderHub" (SQL login orderhub_reports, stored
    credentials) and the report "Invoice", and gives the account the partners service uses the Browser role.
    Run on any machine as a Content Manager of the report server.
.EXAMPLE
    .\Publish-Reports.ps1 -ReportPortalUrl http://reports.corp.local/Reports -SqlServer sql01.corp.local `
        -ReportsPassword (Read-Host -AsSecureString) -BrowserAccount 'REPORTS\svc-orderhub-rs'
#>
param(
    [string] $ReportPortalUrl = 'http://reports.corp.local/Reports',
    [pscredential] $Credential,
    [string] $Folder = 'Order Hub',
    [string] $SqlServer = 'sql01.corp.local',
    [string] $DatabaseName = 'OrderHub',
    [Parameter(Mandatory)] [securestring] $ReportsPassword,
    [string] $BrowserAccount
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$api = $ReportPortalUrl.TrimEnd('/') + '/api/v2.0'
$reports = Join-Path (Split-Path $PSScriptRoot -Parent) 'reports'
$auth = if ($Credential) { @{ Credential = $Credential } } else { @{ UseDefaultCredentials = $true } }

function Invoke-Rs([string] $Method, [string] $Path, $Body) {
    $request = @{ Method = $Method; Uri = "$api/$Path"; ContentType = 'application/json; charset=utf-8' } + $auth
    if ($null -ne $Body) { $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6)) }
    Invoke-RestMethod @request
}

function Get-RsItem([string] $Path) {
    try { Invoke-Rs GET "CatalogItems(Path='$([uri]::EscapeDataString($Path))')" $null }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 404) { $null } else { throw } }
}

$folderPath = "/$Folder"
if (-not (Get-RsItem $folderPath)) {
    Invoke-Rs POST 'Folders' @{ Name = $Folder; Path = '/' } | Out-Null
    Write-Host "Created folder $folderPath"
}

$dataSource = @{
    Name                = 'OrderHub'
    Path                = "$folderPath/OrderHub"
    Type                = 'DataSource'
    DataSourceType      = 'SQL'
    ConnectString       = "Data Source=$SqlServer;Initial Catalog=$DatabaseName"
    CredentialRetrieval = 'Store'
    IsEnabled           = $true
    CredentialsInServer = @{
        UserName                = 'orderhub_reports'
        Password                = (ConvertTo-PlainText $ReportsPassword)
        UseAsWindowsCredentials = $false
        ImpersonateAuthenticatedUser = $false
    }
}
$existing = Get-RsItem $dataSource.Path
if ($existing) { Invoke-Rs PATCH "DataSources($($existing.Id))" $dataSource | Out-Null }
else { Invoke-Rs POST 'DataSources' $dataSource | Out-Null }
Write-Host "Data source $($dataSource.Path)"

foreach ($rdl in Get-ChildItem $reports -Filter *.rdl) {
    $name = $rdl.BaseName
    $report = @{
        Name    = $name
        Path    = "$folderPath/$name"
        Type    = 'Report'
        Content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($rdl.FullName))
    }
    $existing = Get-RsItem $report.Path
    if ($existing) { Invoke-Rs PATCH "Reports($($existing.Id))" $report | Out-Null; $id = $existing.Id }
    else { $id = (Invoke-Rs POST 'Reports' $report).Id }

    # Point the report's data source to the shared one.
    Invoke-Rs PUT "Reports($id)/DataSources" @(@{ Name = 'OrderHub'; IsReference = $true; Path = $dataSource.Path }) | Out-Null
    Write-Host "Report $($report.Path)"
}

if ($BrowserAccount) {
    $folderItem = Get-RsItem $folderPath
    $policies = Invoke-Rs GET "Folders($($folderItem.Id))/Policies" $null
    $policies.InheritParentPolicy = $false
    if (-not ($policies.Policies | Where-Object { $_.GroupUserName -eq $BrowserAccount })) {
        $policies.Policies += @{ GroupUserName = $BrowserAccount; Roles = @(@{ Name = 'Browser' }) }
    }
    Invoke-Rs PUT "Folders($($folderItem.Id))/Policies" $policies | Out-Null
    Write-Host "Browser role for $BrowserAccount on $folderPath"
}
