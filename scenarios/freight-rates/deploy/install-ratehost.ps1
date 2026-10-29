<#
.SYNOPSIS
    Installs the Freight Rates rating host as a Windows service on the rating server.
.DESCRIPTION
    Prerequisites on this server:
      - Meridian RateEngine 7.2 SDK installed (MRS_RateEngine_7.2.1184_x86.msi)
      - Meridian licence key attached and its driver installed
      - .NET 8 runtime (x86) and ASP.NET Core 8 runtime (x86)
      - Server certificate for the rating host name in LocalMachine\My
    Publish first:
      dotnet publish src\FreightRates.RatingHost -c Release -r win-x86 --self-contained false -o publish\ratinghost
.EXAMPLE
    .\install-ratehost.ps1 -PublishPath ..\publish\ratinghost -ServiceAccount 'CORP\svc-ratehost' -AdminPortalAccount 'CORP\svc-freightrates'
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $InstallPath = 'C:\Services\FreightRates.RatingHost',
    [string] $DataRoot = 'D:\MRS',
    [string] $SdkPath = 'C:\Program Files (x86)\Meridian\RateEngine 7.2',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [Parameter(Mandatory)] [string] $AdminPortalAccount,
    [string] $LicenceFile,
    [string] $ServiceName = 'FreightRates.RatingHost',
    [int] $GrpcPort = 5101,
    [int] $HealthPort = 5100,
    [string] $CertificateSubject = 'ratehost01.corp.local'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $SdkPath 'bin\MRSRATE32.DLL'))) {
    throw "RateEngine SDK not found in $SdkPath. Install MRS_RateEngine_7.2.1184_x86.msi first."
}
$dotnetX86 = Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnetX86)) {
    throw '.NET 8 x86 runtime not found. Install the x86 ASP.NET Core 8 runtime.'
}

$keyUtil = Join-Path $SdkPath 'bin\mrsutil.exe'
& $keyUtil key status
if ($LASTEXITCODE -ne 0) {
    throw 'Licence key not detected. Attach the Meridian licence key and check the key driver.'
}

# Data folders
foreach ($folder in 'Tariffs', 'Licence', 'Incoming', 'Archive') {
    New-Item -ItemType Directory -Force -Path (Join-Path $DataRoot $folder) | Out-Null
}
if ($LicenceFile) {
    Copy-Item $LicenceFile (Join-Path $DataRoot 'Licence\contoso.mrl') -Force
}

icacls $DataRoot /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null
icacls (Join-Path $DataRoot 'Incoming') /grant "${AdminPortalAccount}:(OI)(CI)M" | Out-Null

# Share for tariff uploads from the admin portal
if (-not (Get-SmbShare -Name 'MRSData$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'MRSData$' -Path $DataRoot -ChangeAccess $AdminPortalAccount -FullAccess 'BUILTIN\Administrators' | Out-Null
}

# Binaries
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force
}
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force

# Licence password, protected for this machine
$password = Read-Host -AsSecureString 'RateEngine licence password'
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
$exe = Join-Path $InstallPath 'FreightRates.RatingHost.exe'
$encrypted = (& $exe protect-password $plain | Select-Object -Last 1).Trim()

# Service
if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    $credential = Get-Credential -UserName $ServiceAccount -Message 'Rating host service account'
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Freight Rates Rating Host' `
        -Description 'Rates LTL shipments with Meridian RateEngine for the Freight Rates API.' `
        -StartupType Automatic -Credential $credential | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}

# Log on as a service right for the service account
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$rights = Join-Path $env:TEMP 'ratehost-rights.inf'
secedit.exe /export /cfg $rights /areas USER_RIGHTS | Out-Null
$line = Get-Content $rights -Encoding Unicode | Where-Object { $_ -like 'SeServiceLogonRight*' }
if (-not $line -or $line -notlike "*$sid*") {
    $new = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
    @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $new) | Set-Content $rights -Encoding Unicode
    secedit.exe /configure /db (Join-Path $env:TEMP 'ratehost-rights.sdb') /cfg $rights /areas USER_RIGHTS | Out-Null
}
Remove-Item $rights -ErrorAction SilentlyContinue

$envKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
New-ItemProperty -Path $envKey -Name Environment -PropertyType MultiString -Force -Value @(
    'ASPNETCORE_ENVIRONMENT=Production',
    "RatingEngine__SdkPath=$SdkPath",
    "RatingEngine__DataPath=$(Join-Path $DataRoot 'Tariffs')",
    "RatingEngine__LicenceFile=$(Join-Path $DataRoot 'Licence\contoso.mrl')",
    "RatingEngine__IncomingPath=$(Join-Path $DataRoot 'Incoming')",
    "RatingEngine__ArchivePath=$(Join-Path $DataRoot 'Archive')",
    "RatingEngine__EncryptedPassword=$encrypted",
    "Kestrel__Endpoints__Grpc__Url=https://*:$GrpcPort",
    "Kestrel__Endpoints__Grpc__Certificate__Subject=$CertificateSubject",
    "Kestrel__Endpoints__Health__Url=http://*:$HealthPort"
) | Out-Null

# Event log source
if (-not [System.Diagnostics.EventLog]::SourceExists('Freight Rates Rating Host')) {
    New-EventLog -LogName Application -Source 'Freight Rates Rating Host'
}

# Firewall
foreach ($port in $GrpcPort, $HealthPort) {
    $rule = "Freight Rates Rating Host $port"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $port -Action Allow | Out-Null
    }
}

# The service account needs to read the certificate private key
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$CertificateSubject" } | Select-Object -First 1
if (-not $cert) {
    throw "Certificate CN=$CertificateSubject not found in LocalMachine\My."
}
$keyName = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert).Key.UniqueName
# CSP keys are under RSA\MachineKeys, CNG (KSP) keys under Keys.
$keyPath = 'RSA\MachineKeys', 'Keys' |
    ForEach-Object { Join-Path $env:ProgramData "Microsoft\Crypto\$_\$keyName" } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $keyPath) { throw "Private key file $keyName of CN=$CertificateSubject not found." }
icacls $keyPath /grant "${ServiceAccount}:R" | Out-Null

Start-Service $ServiceName
Start-Sleep -Seconds 5
Invoke-RestMethod "http://localhost:$HealthPort/health" | ConvertTo-Json -Depth 5
