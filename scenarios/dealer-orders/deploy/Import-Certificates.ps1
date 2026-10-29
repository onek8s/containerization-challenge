<#
.SYNOPSIS
    Imports the AS2 certificate of Tailspin (with private key) into LocalMachine\My on edi01, gives the EDI service
    account read access to the private key, and copies the dealers' AS2 certificates to the partner folder.
.EXAMPLE
    .\Import-Certificates.ps1 -PfxFile .\tailspin-as2-2019.pfx -PfxPassword $p -ServiceAccount CORP\svc-dealer-edi -PartnerCertificates .\partners
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PfxFile,
    [Parameter(Mandatory)][securestring]$PfxPassword,
    [Parameter(Mandatory)][string]$ServiceAccount,
    [string]$PartnerCertificates
)

. "$PSScriptRoot\common.ps1"
Assert-Admin

Write-Step 'Tailspin AS2 certificate'
$cert = Import-PfxCertificate -FilePath $PfxFile -CertStoreLocation Cert:\LocalMachine\My -Password $PfxPassword -Exportable:$false
Write-Host "  $($cert.Subject) $($cert.Thumbprint), valid to $($cert.NotAfter.ToString('yyyy-MM-dd'))"

$key = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
if ($key -is [Security.Cryptography.RSACng]) {
    $keyFile = Join-Path "$env:ProgramData\Microsoft\Crypto\Keys" $key.Key.UniqueName
} else {
    $keyFile = Join-Path "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys" $cert.PrivateKey.CspKeyContainerInfo.UniqueKeyContainerName
}
icacls $keyFile /grant "${ServiceAccount}:R" | Out-Null
Write-Host "  private key readable by $ServiceAccount"
Write-Host "  Set Edi.As2.CertificateThumbprint = $($cert.Thumbprint)"

if ($PartnerCertificates) {
    Write-Step 'Dealer AS2 certificates'
    $target = Join-Path $InstallRoot 'Edi\Certificates'
    New-Item $target -ItemType Directory -Force | Out-Null
    foreach ($file in Get-ChildItem $PartnerCertificates -Filter '*.cer') {
        Copy-Item $file.FullName $target -Force
        $c = New-Object Security.Cryptography.X509Certificates.X509Certificate2($file.FullName)
        Write-Host "  $($file.Name): $($c.Subject), valid to $($c.NotAfter.ToString('yyyy-MM-dd'))"
    }
}
