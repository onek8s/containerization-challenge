<#
.SYNOPSIS
    Installs the staff back office (housing.corp.local).
.DESCRIPTION
    Prerequisites: IIS with the WebSocket Protocol feature and the ASP.NET Core 5.0 Hosting Bundle (5.0.17), the gMSA
    CORP\svc-housing-web$ installed on this server (Install-ADServiceAccount), the identity server, the document service
    and the database. The app pool runs as the gMSA, which is also its SQL Server login.
    Letter templates are read from D:\HousingData\templates (copied from the published Templates folder on first install).
.EXAMPLE
    .\install-back-office.ps1 -ClientSecret $s2 -PaystreamToken $t -DocumentServiceKey $d -CertificateThumbprint 3F2A...
#>
[CmdletBinding()]
param(
    [string] $HostName = 'housing.corp.local',
    [string] $Authority = 'https://identity.corp.local',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [string] $ServiceAccount = 'CORP\svc-housing-web$',
    [Parameter(Mandatory)] [securestring] $ClientSecret,
    [Parameter(Mandatory)] [securestring] $PaystreamToken,
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
    ConnectionStrings = @{ Housing = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=SSPI;Application Name=Fabrikam Housing Back Office;Encrypt=True;TrustServerCertificate=True" }
    Authentication = [ordered]@{ Authority = $Authority; ClientSecret = (ConvertTo-PlainText $ClientSecret) }
    DataProtection = @{ KeyFolder = "$DataRoot\keys\housing"; ProtectWithDpapi = $true }
    Integration = [ordered]@{
        ProxyUrl = $ProxyUrl
        Payments = @{ AccessToken = (ConvertTo-PlainText $PaystreamToken) }
        DocumentService = @{ BaseUrl = $DocumentServiceUrl; ApiKey = (ConvertTo-PlainText $DocumentServiceKey) }
    }
    Documents = @{ TemplateFolder = "$DataRoot\templates"; LogoFile = "$DataRoot\templates\fabrikam-logo.png" }
    Logging = @{ Folder = "$LogRoot\housing" }
}

Install-HousingWebApp -Name 'housing' -ProjectName 'Fabrikam.Housing.BackOffice' -HostName $HostName -RuntimeVersion '5.0.17' `
    -Identity $ServiceAccount -Settings $settings -DataFolders @("$DataRoot\keys\housing", "$LogRoot\housing") `
    -CertificateThumbprint $CertificateThumbprint | Out-Null

$templates = "$DataRoot\templates"
if (-not (Test-Path $templates)) {
    Copy-Item -Path 'C:\inetpub\sites\housing\Templates' -Destination $templates -Recurse
    Grant-HousingFolder -Path $templates -Account $ServiceAccount -Rights RX
}
Test-WkHtmlToPdf -Target 'C:\inetpub\sites\housing'
