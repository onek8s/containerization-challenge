<#
.SYNOPSIS
    Installs the jobs host (housing-jobs.corp.local): Hangfire server and dashboard for the scheduled jobs.
.DESCRIPTION
    Prerequisites: IIS with Application Initialization and the ASP.NET Core 5.0 Hosting Bundle (5.0.17), the gMSA
    CORP\svc-housing-jobs$ installed on this server, the identity server, the document service and the database.
    The app pool is AlwaysRunning with preload so that the recurring jobs keep running without requests.
    The Bacs bureau SFTP key (PuTTY .ppk) is copied to D:\HousingData\keys\bacs and readable only by the gMSA.
    The council housing benefit schedules are dropped into the HBInbound$ share; letters for the post room are
    written to the HousingPrint$ share.
.EXAMPLE
    .\install-jobs.ps1 -ClientSecret $s3 -PaystreamToken $t -SmsApiKey $k -DocumentServiceKey $d `
        -BacsKeyFile .\fabrikam_bureau.ppk -BacsKeyPassphrase $p -BacsHostKeyFingerprint 'ssh-rsa 2048 3b:...'
#>
[CmdletBinding()]
param(
    [string] $HostName = 'housing-jobs.corp.local',
    [string] $Authority = 'https://identity.corp.local',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [string] $ServiceAccount = 'CORP\svc-housing-jobs$',
    [Parameter(Mandatory)] [securestring] $ClientSecret,
    [Parameter(Mandatory)] [securestring] $PaystreamToken,
    [Parameter(Mandatory)] [securestring] $SmsApiKey,
    [Parameter(Mandatory)] [securestring] $DocumentServiceKey,
    [string] $BacsKeyFile,
    [securestring] $BacsKeyPassphrase,
    [string] $BacsHostKeyFingerprint,
    [string] $DocumentServiceUrl = 'http://docs01.corp.local:8085/',
    [string] $ProxyUrl = 'http://proxy.corp.local:8080',
    [string] $TimeZone = 'GMT Standard Time',
    [string] $IncomeTeamGroup = 'CORP\HSG-Income',
    [string] $PostRoomGroup = 'CORP\HSG-PostRoom',
    [string] $CertificateThumbprint,
    [string] $DataRoot = 'D:\HousingData',
    [string] $InterfaceRoot = 'D:\Interfaces',
    [string] $LogRoot = 'D:\Logs\Housing'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force

$bacs = [ordered]@{ PrivateKeyFile = "$DataRoot\keys\bacs\fabrikam_bureau.ppk" }
if ($BacsKeyPassphrase) { $bacs.PrivateKeyPassphrase = ConvertTo-PlainText $BacsKeyPassphrase }
if ($BacsHostKeyFingerprint) { $bacs.HostKeyFingerprint = $BacsHostKeyFingerprint }

$settings = [ordered]@{
    ConnectionStrings = @{ Housing = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=SSPI;Application Name=Fabrikam Housing Jobs;Encrypt=True;TrustServerCertificate=True" }
    Authentication = [ordered]@{ Authority = $Authority; ClientSecret = (ConvertTo-PlainText $ClientSecret) }
    Jobs = [ordered]@{
        TimeZone = $TimeZone
        PrintFolder = "$DataRoot\print"
        HousingBenefitInbound = "$InterfaceRoot\HB\Inbound"
        FinanceOutbound = "$InterfaceRoot\Finance\Outbound"
        BacsArchiveFolder = "$InterfaceRoot\Bacs\Sent"
    }
    Integration = [ordered]@{
        ProxyUrl = $ProxyUrl
        Payments = @{ AccessToken = (ConvertTo-PlainText $PaystreamToken) }
        Sms = @{ ApiKey = (ConvertTo-PlainText $SmsApiKey) }
        BacsBureau = $bacs
        DocumentService = @{ BaseUrl = $DocumentServiceUrl; ApiKey = (ConvertTo-PlainText $DocumentServiceKey) }
    }
    DataProtection = @{ KeyFolder = "$DataRoot\keys\jobs"; ProtectWithDpapi = $true }
    Logging = @{ Folder = "$LogRoot\jobs" }
}

$folders = @(
    "$DataRoot\keys\jobs", "$DataRoot\print", "$LogRoot\jobs",
    "$InterfaceRoot\HB\Inbound", "$InterfaceRoot\HB\Processed", "$InterfaceRoot\HB\Failed",
    "$InterfaceRoot\Finance\Outbound", "$InterfaceRoot\Bacs\Sent")
Install-HousingWebApp -Name 'housing-jobs' -ProjectName 'Fabrikam.Housing.Jobs' -HostName $HostName -RuntimeVersion '5.0.17' `
    -Identity $ServiceAccount -Settings $settings -DataFolders $folders -CertificateThumbprint $CertificateThumbprint -AlwaysRunning | Out-Null
Test-WkHtmlToPdf -Target 'C:\inetpub\sites\housing-jobs'

if ($BacsKeyFile) {
    $keyFolder = "$DataRoot\keys\bacs"
    New-Item -ItemType Directory -Path $keyFolder -Force | Out-Null
    Copy-Item $BacsKeyFile (Join-Path $keyFolder 'fabrikam_bureau.ppk') -Force
    & icacls $keyFolder /inheritance:r /grant:r 'BUILTIN\Administrators:(OI)(CI)F' "${ServiceAccount}:(OI)(CI)R" /T | Out-Null
}

# Shares for the council housing benefit schedules and the post room print queue.
if (-not (Get-SmbShare -Name 'HBInbound$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'HBInbound$' -Path "$InterfaceRoot\HB\Inbound" -ChangeAccess $IncomeTeamGroup -FullAccess 'BUILTIN\Administrators' | Out-Null
}
Grant-HousingFolder -Path "$InterfaceRoot\HB\Inbound" -Account $IncomeTeamGroup
if (-not (Get-SmbShare -Name 'HousingPrint$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'HousingPrint$' -Path "$DataRoot\print" -ChangeAccess $PostRoomGroup -FullAccess 'BUILTIN\Administrators' | Out-Null
}
Grant-HousingFolder -Path "$DataRoot\print" -Account $PostRoomGroup

Write-Host "Hangfire dashboard: https://$HostName/hangfire (HSG-Admins, HSG-Finance)"
