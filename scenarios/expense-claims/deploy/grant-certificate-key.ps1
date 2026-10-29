<#
.SYNOPSIS
    Gives an account read access to the private key of a certificate in LocalMachine\My.
.DESCRIPTION
    Works for keys in a CNG key storage provider (Microsoft Software Key Storage Provider) and for
    legacy CryptoAPI keys (certificates imported from a .pfx made with older tools).
.EXAMPLE
    .\grant-certificate-key.ps1 -Thumbprint 9C1E... -Identity 'IIS AppPool\expenses-jobs'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Thumbprint,
    [Parameter(Mandatory)] [string] $Identity
)

$ErrorActionPreference = 'Stop'

$cert = Get-Item "Cert:\LocalMachine\My\$($Thumbprint -replace '\s', '')"
if (-not $cert.HasPrivateKey) { throw "Certificate $Thumbprint has no private key." }

$rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
if ($rsa -is [Security.Cryptography.RSACng]) {
    $keyName = $rsa.Key.UniqueName
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$keyName"
} else {
    $keyName = $cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
    $keyPath = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$keyName"
}
if (-not (Test-Path $keyPath)) { throw "Private key file $keyPath was not found." }

& icacls $keyPath /grant "${Identity}:R" | Out-Null
Write-Host "Granted $Identity read access to the private key of $($cert.Subject)"
