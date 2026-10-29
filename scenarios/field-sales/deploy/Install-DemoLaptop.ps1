<#
.SYNOPSIS
    Prepares a Windows 10/11 laptop (or the lab server itself) to install Field Sales: trusts the web and code
    signing certificates and adds the deployment site to the Local intranet zone so that ClickOnce runs it.
    Run as administrator on the laptop.
.EXAMPLE
    .\Install-DemoLaptop.ps1 -ZoneName corp.local -WebCertificate \\host01\c$\Deploy\FieldSales\fieldsales-web.cer `
        -SigningCertificate \\host01\c$\Deploy\FieldSales\fieldsales-codesigning.cer
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$WebCertificate,
    [string]$SigningCertificate
)

$ErrorActionPreference = 'Stop'

if ($WebCertificate) { Import-Certificate -FilePath $WebCertificate -CertStoreLocation Cert:\LocalMachine\Root | Out-Null }
if ($SigningCertificate) {
    Import-Certificate -FilePath $SigningCertificate -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
    Import-Certificate -FilePath $SigningCertificate -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
}

$domains = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\ZoneMap\Domains'
$key = Join-Path $domains "$ZoneName\salesapp"
New-Item -Path $key -Force | Out-Null
New-ItemProperty -Path $key -Name https -Value 1 -PropertyType DWord -Force | Out-Null

Write-Host "Ready. Install Field Sales from https://salesapp.$ZoneName/publish.htm"
