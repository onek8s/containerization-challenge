<#
.SYNOPSIS
    Imports a PFX into LocalMachine\My and lets the given accounts read its private key.
.DESCRIPTION
    Used for the HTTPS certificates of the IIS sites, the token signing certificate (login) and the
    customs client certificate (CarrierGateway). The accounts are app pool identities
    ("IIS AppPool\ControlTower-login") or service accounts.
.EXAMPLE
    .\Import-Certificates.ps1 -PfxFile .\login-signing.pfx -Password $pfxPw -GrantRead 'IIS AppPool\ControlTower-login'
.EXAMPLE
    .\Import-Certificates.ps1 -PfxFile .\customs-client.pfx -Password $pfxPw -GrantRead '.\svc-ct-gateway'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PfxFile,
    [Parameter(Mandatory)][securestring]$Password,
    [string[]]$GrantRead = @(),
    [switch]$Exportable
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

Write-Step "Import $PfxFile"
$params = @{ FilePath = $PfxFile; CertStoreLocation = 'Cert:\LocalMachine\My'; Password = $Password }
if ($Exportable) { $params.Exportable = $true }
$cert = Import-PfxCertificate @params
Write-Host "  $($cert.Subject)  thumbprint $($cert.Thumbprint)  valid to $($cert.NotAfter)"

if ($GrantRead.Count -eq 0) { return $cert.Thumbprint }

Write-Step 'Private key access'
$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
if ($rsa -is [System.Security.Cryptography.RSACng]) {
    $keyName = $rsa.Key.UniqueName
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$keyName"
} else {
    $keyName = $cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$keyName"
}
if (-not (Test-Path $keyPath)) { throw "Private key file $keyPath not found." }

$acl = Get-Acl $keyPath
foreach ($account in $GrantRead) {
    $rule = New-Object Security.AccessControl.FileSystemAccessRule($account, 'Read', 'Allow')
    $acl.AddAccessRule($rule)
    Write-Host "  $account"
}
Set-Acl -Path $keyPath -AclObject $acl
return $cert.Thumbprint
