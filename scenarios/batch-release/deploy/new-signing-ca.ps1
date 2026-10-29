<#
.SYNOPSIS
    Creates the Batch Release signing CA certificate in LocalMachine\My on the signing host.
.DESCRIPTION
    The CA issues the Qualified Persons' signer certificates during enrolment. Its public certificate
    (exported to -ExportPath) is given to ERP and to the market release partners so they can verify
    batch certificates. Where the corporate PKI issues a subordinate CA certificate for this purpose,
    import it instead and use its thumbprint.
.EXAMPLE
    .\new-signing-ca.ps1 -ExportPath C:\Temp\batch-release-signing-ca.cer
#>
[CmdletBinding()]
param(
    [string] $Subject = 'CN=Proseware Batch Release Signing CA, O=Proseware Pharmaceuticals Oy, C=FI',
    [int] $Years = 10,
    [string] $KeyStorageProvider = 'Microsoft Software Key Storage Provider',
    [string] $ExportPath
)

$ErrorActionPreference = 'Stop'

$ca = New-SelfSignedCertificate -Subject $Subject -CertStoreLocation 'Cert:\LocalMachine\My' `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -Provider $KeyStorageProvider `
    -KeyExportPolicy NonExportable -KeyUsage CertSign, CRLSign, DigitalSignature -KeyUsageProperty Sign `
    -TextExtension @('2.5.29.19={critical}{text}ca=true&pathlength=0') -NotAfter (Get-Date).AddYears($Years)

if ($ExportPath) {
    Export-Certificate -Cert $ca -FilePath $ExportPath | Out-Null
    Write-Host "Public certificate exported to $ExportPath"
}
Write-Host "Signing CA thumbprint: $($ca.Thumbprint)"
