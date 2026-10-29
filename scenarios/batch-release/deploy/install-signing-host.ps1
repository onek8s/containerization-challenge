<#
.SYNOPSIS
    Installs the Batch Release signing host as a Windows service on release-sign.corp.local.
.DESCRIPTION
    Prerequisites: .NET 8 runtime and ASP.NET Core 8 runtime (x64), a server certificate for the host name
    in LocalMachine\My, the vault created with create-vault.ps1, the signing CA from new-signing-ca.ps1,
    and a TPM 2.0 (Get-Tpm) for the Microsoft Platform Crypto Provider.

    The service account:
      - gets 'Log on as a service' and 'Generate security audits' (grant-user-right.ps1),
      - must be a member of 'Windows Authorization Access Group' in the domain so access checks can read
        the token groups of the users who open records,
      - needs the SPN HTTP/<host> for Kerberos from the IIS application pools.

    Success and failure auditing for 'Application Generated' is switched on with auditpol; signatures,
    rejected signatures and record access go to the Security log under the source 'Proseware Batch Release'.

    On servers without a TPM pass -KeyStorageProvider 'Microsoft Software Key Storage Provider' and
    -AllowSoftwareKeys.
.EXAMPLE
    .\install-signing-host.ps1 -ServiceAccount 'CORP\svc-release-sign' -ServicePassword $pw `
        -CertificateThumbprint 8C1D... -IssuerThumbprint 51AF... `
        -TrustedCallers 'CORP\svc-release-web','CORP\svc-release-api'
#>
[CmdletBinding()]
param(
    [string] $InstallPath = 'C:\Services\BatchReleaseSigning',
    [string] $HostName = 'release-sign.corp.local',
    [int] $Port = 5443,
    [string] $SqlInstance = 'release-sql.corp.local',
    [string] $DatabaseName = 'BatchRelease',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [Parameter(Mandatory)] [securestring] $ServicePassword,
    [Parameter(Mandatory)] [string] $CertificateThumbprint,
    [Parameter(Mandatory)] [string] $IssuerThumbprint,
    [Parameter(Mandatory)] [string[]] $TrustedCallers,
    [string] $DefaultDomain = 'CORP',
    [string] $VaultPath = 'D:\ReleaseVault',
    [string] $KeyStorageProvider = 'Microsoft Platform Crypto Provider',
    [switch] $AllowSoftwareKeys
)

$ErrorActionPreference = 'Stop'
$serviceName = 'BatchReleaseSigning'
$src = Join-Path $PSScriptRoot '..\src\BatchRelease.SigningHost\BatchRelease.SigningHost.csproj'
$url = "https://${HostName}:$Port/"

if ($KeyStorageProvider -eq 'Microsoft Platform Crypto Provider') {
    $tpm = Get-Tpm
    if (-not ($tpm.TpmPresent -and $tpm.TpmReady)) { throw 'TPM is not present or not ready.' }
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') { Stop-Service -Name $serviceName -Force }

& dotnet publish $src -c Release -o $InstallPath
if ($LASTEXITCODE -ne 0) { throw 'Publishing the signing host failed.' }

@{
    ConnectionStrings = @{ BatchRelease = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=Batch Release Signing" }
    SigningHost = @{ Urls = $url }
    Signing = @{
        KeyStorageProvider = $KeyStorageProvider
        RequireHardwareKeys = -not $AllowSoftwareKeys.IsPresent
        IssuerThumbprint = $IssuerThumbprint
        DefaultDomain = $DefaultDomain
        TrustedCallers = $TrustedCallers
    }
    Vault = @{ Root = $VaultPath }
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $InstallPath 'appsettings.Production.json') -Encoding UTF8

# Rights for the service account (merged into the existing assignments).
& (Join-Path $PSScriptRoot 'grant-user-right.ps1') -Account $ServiceAccount -Right SeServiceLogonRight
& (Join-Path $PSScriptRoot 'grant-user-right.ps1') -Account $ServiceAccount -Right SeAuditPrivilege

# Security auditing of application-generated events, and the event source.
& auditpol.exe /set /subcategory:"Application Generated" /success:enable /failure:enable | Out-Null
& (Join-Path $InstallPath 'BatchRelease.SigningHost.exe') install-audit-source
if ($LASTEXITCODE -ne 0) { throw 'Installing the security event source failed.' }

if (Get-Command Add-ADGroupMember -ErrorAction SilentlyContinue) {
    $sam = $ServiceAccount.Split('\')[-1]
    Add-ADGroupMember -Identity 'Windows Authorization Access Group' -Members $sam -ErrorAction SilentlyContinue
    & setspn.exe -S "HTTP/$HostName" $ServiceAccount | Out-Null
}

# HTTP.sys URL reservation and TLS binding.
& netsh.exe http delete urlacl url=$url 2>$null | Out-Null
& netsh.exe http add urlacl url=$url user=$ServiceAccount | Out-Null
& netsh.exe http delete sslcert hostnameport="${HostName}:$Port" 2>$null | Out-Null
& netsh.exe http add sslcert hostnameport="${HostName}:$Port" certhash=$CertificateThumbprint certstorename=MY appid='{4f6d1c3e-8a52-4d0e-9b7a-2f1f5c0b7a11}' | Out-Null
if (-not (Get-NetFirewallRule -DisplayName 'Batch Release Signing' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Batch Release Signing' -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}

$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ServicePassword))
$exe = Join-Path $InstallPath 'BatchRelease.SigningHost.exe'
if (-not $existing) {
    & sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $plain DisplayName= 'Batch Release Signing' | Out-Null
} else {
    & sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $plain | Out-Null
}
& sc.exe description $serviceName 'Signs batch decisions and keeps the batch record vault.' | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/none/0 | Out-Null
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')

# The service account must have created its EFS certificate before the first file is written:
# log on once as the account, or let the service write its first record.
Start-Service -Name $serviceName
Write-Host "Signing host running at $url as $ServiceAccount."
Write-Host "Enrol Qualified Persons with: & '$exe' enroll --account CORP\user --name 'Full Name'"
