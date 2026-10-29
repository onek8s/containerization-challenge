<#
.SYNOPSIS
    Installs the tenant portal (tenants.corp.local, published as tenants.fabrikamhomes.co.uk by the DMZ reverse proxy).
.DESCRIPTION
    Prerequisites: IIS with the ASP.NET Core 3.1 Hosting Bundle (3.1.32), the identity server, the document service,
    the database, and outbound access to Paystream and the SMS gateway through proxy.corp.local.
    Letters and statements use wkhtmltopdf: copy libwkhtmltox.dll to C:\inetpub\sites\tenants\native after publishing.
.EXAMPLE
    .\install-tenant-portal.ps1 -SqlPassword $pw -ClientSecret $s1 -PaystreamToken $t -SmsApiKey $k -DocumentServiceKey $d
#>
[CmdletBinding()]
param(
    [string] $HostName = 'tenants.corp.local',
    [string] $Authority = 'https://identity.corp.local',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [Parameter(Mandatory)] [securestring] $ClientSecret,
    [Parameter(Mandatory)] [securestring] $PaystreamToken,
    [Parameter(Mandatory)] [securestring] $SmsApiKey,
    [Parameter(Mandatory)] [securestring] $DocumentServiceKey,
    [string] $DocumentServiceUrl = 'http://docs01.corp.local:8085/',
    [string] $ProxyUrl = 'http://proxy.corp.local:8080',
    [string] $CertificateThumbprint,
    [string] $DataRoot = 'D:\HousingData',
    [string] $LogRoot = 'D:\Logs\Housing'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force

$settings = [ordered]@{
    ConnectionStrings = @{ Housing = "Server=$SqlInstance;Database=$DatabaseName;User Id=housing_portal;Password=$(ConvertTo-PlainText $SqlPassword);Application Name=Fabrikam Tenant Portal" }
    Authentication = [ordered]@{ Authority = $Authority; ClientSecret = (ConvertTo-PlainText $ClientSecret) }
    DataProtection = @{ KeyFolder = "$DataRoot\keys\tenants"; ProtectWithDpapi = $true }
    Uploads = @{ TempFolder = "$DataRoot\uploads" }
    Integration = [ordered]@{
        ProxyUrl = $ProxyUrl
        Payments = @{ AccessToken = (ConvertTo-PlainText $PaystreamToken) }
        Sms = @{ ApiKey = (ConvertTo-PlainText $SmsApiKey) }
        DocumentService = @{ BaseUrl = $DocumentServiceUrl; ApiKey = (ConvertTo-PlainText $DocumentServiceKey) }
    }
    Logging = @{ Folder = "$LogRoot\tenants" }
}

Install-HousingWebApp -Name 'tenants' -ProjectName 'Fabrikam.Housing.TenantPortal' -HostName $HostName -RuntimeVersion '3.1.32' `
    -Settings $settings -DataFolders @("$DataRoot\keys\tenants", "$DataRoot\uploads", "$LogRoot\tenants") `
    -CertificateThumbprint $CertificateThumbprint | Out-Null
Test-WkHtmlToPdf -Target 'C:\inetpub\sites\tenants'
