<#
.SYNOPSIS
    Creates a self-signed code signing certificate for the ClickOnce manifests on the build machine and exports the
    public part. Distribute the .cer to the laptops' Trusted Publishers and Trusted Root stores (Group Policy) so that
    Field Sales installs and updates without a security prompt.
.EXAMPLE
    .\New-SigningCertificate.ps1 -OutputPath C:\Deploy\FieldSales
#>
param(
    [Parameter(Mandatory)] [string]$OutputPath,
    [string]$Subject = 'CN=Wingtip Foods Field Sales'
)

$ErrorActionPreference = 'Stop'
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation Cert:\CurrentUser\My `
    -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(5) -HashAlgorithm SHA256
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
$file = Join-Path $OutputPath 'fieldsales-codesigning.cer'
Export-Certificate -Cert $cert -FilePath $file | Out-Null

Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Public certificate: $file"
Write-Host "Publish with: .\Publish.ps1 ... -SigningThumbprint $($cert.Thumbprint)"
