<#
.SYNOPSIS
    Creates the supplier FTP site on ftp.<zone> (IIS FTP with explicit FTPS). Each supplier gets a local
    or domain account that is isolated to suppliers\<code>; the gateway account reads all supplier folders.
.EXAMPLE
    .\Install-FtpSite.ps1 -Root D:\Ftp -GatewayAccount CORP\svc-inv-ftp -CertificateThumbprint <thumbprint> -Suppliers NF,KTL
#>
param(
    [string]$Root = 'D:\Ftp',
    [string]$SiteName = 'SupplierFtp',
    [Parameter(Mandatory)] [string]$GatewayAccount,
    [Parameter(Mandatory)] [string]$CertificateThumbprint,
    [string[]]$Suppliers = @('NF', 'KTL'),
    [int]$PassivePortLow = 50000,
    [int]$PassivePortHigh = 50100
)

$ErrorActionPreference = 'Stop'
Install-WindowsFeature Web-Ftp-Server, Web-Ftp-Service, Web-Mgmt-Console | Out-Null
Import-Module WebAdministration

New-Item -ItemType Directory -Force -Path $Root | Out-Null
foreach ($code in $Suppliers) {
    foreach ($sub in 'out', 'processed', 'rejected') {
        New-Item -ItemType Directory -Force -Path (Join-Path $Root "suppliers\$code\$sub") | Out-Null
    }
}
icacls (Join-Path $Root 'suppliers') /grant "${GatewayAccount}:(OI)(CI)M" | Out-Null
foreach ($code in $Suppliers) {
    try { [void](New-Object System.Security.Principal.NTAccount("FTP-$code")).Translate([System.Security.Principal.SecurityIdentifier]) }
    catch { Write-Warning "Group FTP-$code not found; grant it Modify on suppliers\$code after creating it."; continue }
    icacls (Join-Path $Root "suppliers\$code") /grant "FTP-${code}:(OI)(CI)M" | Out-Null
}

if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    New-WebFtpSite -Name $SiteName -Port 21 -PhysicalPath $Root | Out-Null
}

$site = "IIS:\Sites\$SiteName"
Set-ItemProperty $site -Name ftpServer.security.authentication.basicAuthentication.enabled -Value $true
Set-ItemProperty $site -Name ftpServer.security.authentication.anonymousAuthentication.enabled -Value $false
Set-ItemProperty $site -Name ftpServer.security.ssl.serverCertHash -Value $CertificateThumbprint
Set-ItemProperty $site -Name ftpServer.security.ssl.controlChannelPolicy -Value 'SslRequire'
Set-ItemProperty $site -Name ftpServer.security.ssl.dataChannelPolicy -Value 'SslRequire'
Set-ItemProperty $site -Name ftpServer.directoryBrowse.showFlags -Value 'DisplayVirtualDirectories'

# Suppliers sign in at the site root (read only) and may write only below suppliers\<code>.
$supplierRoles = ($Suppliers | ForEach-Object { "FTP-$_" }) -join ','
Clear-WebConfiguration -PSPath 'IIS:\' -Location $SiteName -Filter 'system.ftpServer/security/authorization'
Add-WebConfiguration -PSPath 'IIS:\' -Location $SiteName -Filter 'system.ftpServer/security/authorization' `
    -Value @{ accessType = 'Allow'; users = $GatewayAccount; permissions = 'Read,Write' }
Add-WebConfiguration -PSPath 'IIS:\' -Location $SiteName -Filter 'system.ftpServer/security/authorization' `
    -Value @{ accessType = 'Allow'; roles = $supplierRoles; permissions = 'Read' }
foreach ($code in $Suppliers) {
    $location = "$SiteName/suppliers/$code"
    Clear-WebConfiguration -PSPath 'IIS:\' -Location $location -Filter 'system.ftpServer/security/authorization'
    Add-WebConfiguration -PSPath 'IIS:\' -Location $location -Filter 'system.ftpServer/security/authorization' `
        -Value @{ accessType = 'Allow'; roles = "FTP-$code"; permissions = 'Read,Write' }
    $others = ($Suppliers | Where-Object { $_ -ne $code } | ForEach-Object { "FTP-$_" }) -join ','
    if ($others) {
        Add-WebConfiguration -PSPath 'IIS:\' -Location $location -Filter 'system.ftpServer/security/authorization' `
            -Value @{ accessType = 'Deny'; roles = $others; permissions = 'Read,Write' }
    }
}

Set-WebConfigurationProperty -PSPath 'IIS:\' -Filter 'system.ftpServer/firewallSupport' -Name lowDataChannelPort -Value $PassivePortLow
Set-WebConfigurationProperty -PSPath 'IIS:\' -Filter 'system.ftpServer/firewallSupport' -Name highDataChannelPort -Value $PassivePortHigh
if (-not (Get-NetFirewallRule -DisplayName 'Supplier FTP' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Supplier FTP' -Direction Inbound -Protocol TCP -LocalPort 21, "$PassivePortLow-$PassivePortHigh" -Action Allow | Out-Null
}
Restart-Service ftpsvc
Write-Host "FTP site $SiteName on $Root. Create the local groups FTP-<code> and add the supplier accounts to them."
