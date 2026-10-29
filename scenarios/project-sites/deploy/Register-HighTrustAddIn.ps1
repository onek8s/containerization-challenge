<#
.SYNOPSIS
    Registers the trusted security token issuer for the high-trust add-ins and the app principals of the
    dashboards add-in and the sync service, and grants them app-only permissions.
.DESCRIPTION
    Both clients sign their actor tokens with the private key of ProjectSitesS2S.pfx (installed on the add-in
    server and the sync server). SharePoint trusts the public certificate registered here. The farm must
    have the App Management and Subscription Settings service applications and an app domain configured.
.EXAMPLE
    .\Register-HighTrustAddIn.ps1
#>
param([switch] $AllowOAuthOverHttp)

Import-Module (Join-Path $PSScriptRoot 'LucerneProjectSites.psd1') -Force
Assert-SharePointShell
$settings = Get-ProjectSitesSettings

$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($settings.SigningCertificate)
$realm = Get-SPAuthenticationRealm
Write-Step "Authentication realm $realm"

# Root authority so SharePoint trusts the certificate chain.
if (-not (Get-SPTrustedRootAuthority | Where-Object { $_.Certificate.Thumbprint -eq $certificate.Thumbprint })) {
    New-SPTrustedRootAuthority -Name 'Lucerne Project Sites S2S' -Certificate $certificate | Out-Null
    Write-Step 'Added trusted root authority'
}

$issuerNameId = "$($settings.IssuerId)@$realm"
$issuer = Get-SPTrustedSecurityTokenIssuer | Where-Object { $_.NameId -eq $issuerNameId }
if (-not $issuer) {
    New-SPTrustedSecurityTokenIssuer -Name $settings.IssuerName -Certificate $certificate -RegisteredIssuerName $issuerNameId -IsTrustBroker | Out-Null
    Write-Step "Registered token issuer $issuerNameId"
}

if ($AllowOAuthOverHttp) {
    # Test farms only: the production web application is HTTPS.
    $sts = Get-SPSecurityTokenServiceConfig
    $sts.AllowOAuthOverHttp = $true
    $sts.Update()
}

$hub = Get-SPWeb -Identity $settings.HubUrl
try {
    foreach ($client in @(
            @{ Id = $settings.DashboardsClientId; Name = 'Lucerne Project Dashboards'; Right = 'Read' },
            @{ Id = $settings.SyncClientId; Name = 'Lucerne Project Sync'; Right = 'FullControl' })) {
        $principal = Register-SPAppPrincipal -NameIdentifier "$($client.Id)@$realm" -Site $hub -DisplayName $client.Name
        # App-only permission on the whole web application ("site collection" scope is not enough because the
        # clients work across all project site collections).
        Set-SPAppPrincipalPermission -AppPrincipal $principal -Site $hub -Scope SiteSubscription -Right $client.Right
        Write-Step "$($client.Name): $($client.Right) on the subscription"
    }
} finally {
    $hub.Dispose()
}

Write-Step "Set Realm=$realm in the add-in Web.config and the sync service config."
