<#
.SYNOPSIS
    Installs the Fabrikam Housing identity server (identity.corp.local).
.DESCRIPTION
    Prerequisites: IIS with the ASP.NET Core 3.1 Hosting Bundle (3.1.32), .NET SDK 5.0.408 on the machine running the
    script, the token signing certificate in LocalMachine\My, LDAPS reachable on dc01.corp.local:636 and the database
    created with install-database.ps1.
    The client secrets given here must match the ones given to install-tenant-portal.ps1, install-back-office.ps1 and
    install-jobs.ps1.
.EXAMPLE
    .\install-identity.ps1 -SqlPassword $pw -SigningThumbprint 7A1C... -CertificateThumbprint 3F2A... `
        -TenantPortalSecret $s1 -BackOfficeSecret $s2 -JobsSecret $s3
#>
[CmdletBinding()]
param(
    [string] $HostName = 'identity.corp.local',
    [string] $IssuerUri = 'https://identity.corp.local',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [Parameter(Mandatory)] [string] $SigningThumbprint,
    [string] $PreviousSigningThumbprint = '',
    [string] $CertificateThumbprint,
    [Parameter(Mandatory)] [securestring] $TenantPortalSecret,
    [Parameter(Mandatory)] [securestring] $BackOfficeSecret,
    [Parameter(Mandatory)] [securestring] $JobsSecret,
    [string] $TenantPortalUrl = 'https://tenants.fabrikamhomes.co.uk',
    [string] $BackOfficeUrl = 'https://housing.corp.local',
    [string] $JobsUrl = 'https://housing-jobs.corp.local',
    [string] $LdapServer = 'dc01.corp.local',
    [string] $LdapSearchBase = 'OU=Staff,DC=corp,DC=local',
    [string] $DataRoot = 'D:\HousingData',
    [string] $LogRoot = 'D:\Logs\Housing'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force

$sql = "Server=$SqlInstance;Database=$DatabaseName;User Id=housing_identity;Password=$(ConvertTo-PlainText $SqlPassword);Application Name=Fabrikam Identity"
$settings = [ordered]@{
    ConnectionStrings = [ordered]@{ Housing = $sql; Identity = $sql; Grants = $sql }
    IdentityServer = @{ IssuerUri = $IssuerUri }
    Signing = [ordered]@{ Mode = 'Store'; Thumbprint = $SigningThumbprint; PreviousThumbprint = $PreviousSigningThumbprint }
    DataProtection = @{ KeyFolder = "$DataRoot\keys\identity"; ProtectWithDpapi = $true }
    Clients = [ordered]@{
        TenantPortal = @{ BaseUrl = $TenantPortalUrl; Secret = (ConvertTo-PlainText $TenantPortalSecret) }
        BackOffice = @{ BaseUrl = $BackOfficeUrl; Secret = (ConvertTo-PlainText $BackOfficeSecret) }
        Jobs = @{ BaseUrl = $JobsUrl; Secret = (ConvertTo-PlainText $JobsSecret) }
    }
    Ldap = @{ Mode = 'Directory'; Server = $LdapServer; SearchBase = $LdapSearchBase }
    Logging = @{ Folder = "$LogRoot\identity" }
}

$account = Install-HousingWebApp -Name 'identity' -ProjectName 'Fabrikam.Housing.Identity' -HostName $HostName -RuntimeVersion '3.1.32' `
    -Settings $settings -DataFolders @("$DataRoot\keys\identity", "$LogRoot\identity") -CertificateThumbprint $CertificateThumbprint

Grant-CertificatePrivateKey -Thumbprint $SigningThumbprint -Account $account
if ($PreviousSigningThumbprint) { Grant-CertificatePrivateKey -Thumbprint $PreviousSigningThumbprint -Account $account }
