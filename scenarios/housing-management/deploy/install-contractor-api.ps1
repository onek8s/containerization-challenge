<#
.SYNOPSIS
    Installs the contractor API (contractors-api.corp.local, published as contractors-api.fabrikamhomes.co.uk).
.DESCRIPTION
    Prerequisites: IIS with the ASP.NET Core 5.0 Hosting Bundle (5.0.17), the identity server, the document service and
    the database. Paystream calls POST /webhooks/paystream through the reverse proxy; the webhook secret comes from the
    Paystream dashboard.
.EXAMPLE
    .\install-contractor-api.ps1 -SqlPassword $pw -PaystreamToken $t -WebhookSecret $w -SmsApiKey $k -DocumentServiceKey $d
#>
[CmdletBinding()]
param(
    [string] $HostName = 'contractors-api.corp.local',
    [string] $Authority = 'https://identity.corp.local',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [Parameter(Mandatory)] [securestring] $PaystreamToken,
    [Parameter(Mandatory)] [securestring] $WebhookSecret,
    [Parameter(Mandatory)] [securestring] $SmsApiKey,
    [Parameter(Mandatory)] [securestring] $DocumentServiceKey,
    [string] $DocumentServiceUrl = 'http://docs01.corp.local:8085/',
    [string] $ProxyUrl = 'http://proxy.corp.local:8080',
    [int] $RequestsPerMinute = 120,
    [string] $CertificateThumbprint,
    [string] $DataRoot = 'D:\HousingData',
    [string] $LogRoot = 'D:\Logs\Housing'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force

$settings = [ordered]@{
    ConnectionStrings = @{ Housing = "Server=$SqlInstance;Database=$DatabaseName;User Id=housing_api;Password=$(ConvertTo-PlainText $SqlPassword);Application Name=Fabrikam Contractor API;Encrypt=True;TrustServerCertificate=True" }
    Authentication = @{ Authority = $Authority }
    ApiLimits = @{ RequestsPerMinute = $RequestsPerMinute }
    DataProtection = @{ KeyFolder = "$DataRoot\keys\contractor-api"; ProtectWithDpapi = $true }
    Integration = [ordered]@{
        ProxyUrl = $ProxyUrl
        Payments = @{ AccessToken = (ConvertTo-PlainText $PaystreamToken); WebhookSecret = (ConvertTo-PlainText $WebhookSecret) }
        Sms = @{ ApiKey = (ConvertTo-PlainText $SmsApiKey) }
        DocumentService = @{ BaseUrl = $DocumentServiceUrl; ApiKey = (ConvertTo-PlainText $DocumentServiceKey) }
    }
    Logging = @{ Folder = "$LogRoot\contractor-api" }
}

Install-HousingWebApp -Name 'contractors-api' -ProjectName 'Fabrikam.Housing.ContractorApi' -HostName $HostName -RuntimeVersion '5.0.17' `
    -Settings $settings -DataFolders @("$DataRoot\keys\contractor-api", "$LogRoot\contractor-api") `
    -CertificateThumbprint $CertificateThumbprint | Out-Null
