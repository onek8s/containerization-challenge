<#
.SYNOPSIS
    Installs the document service on docs01.corp.local as the Windows service FabrikamDocumentService.
.DESCRIPTION
    Prerequisites: .NET Core 3.1 Runtime (3.1.32, x64) and ASP.NET Core 3.1 Runtime, the gMSA CORP\svc-housing-docs$
    installed on this server, the database, and a data disk D:.
    The service listens on http://docs01.corp.local:8085 for the other applications (X-Api-Key). The post room
    scanners (MFDs) save to \\docs01\Scans$, which is D:\Scans\Inbound.
.EXAMPLE
    .\install-document-service.ps1 -ApiKeys (Read-Host -AsSecureString 'tenants'), (Read-Host -AsSecureString 'housing') ...
#>
[CmdletBinding()]
param(
    [string] $InstallPath = 'C:\Services\FabrikamDocumentService',
    [string] $ServiceName = 'FabrikamDocumentService',
    [string] $ServiceAccount = 'CORP\svc-housing-docs$',
    [string] $SqlInstance = 'sql01.corp.local',
    [string] $DatabaseName = 'FabrikamHousing',
    [Parameter(Mandatory)] [securestring[]] $ApiKeys,
    [int] $Port = 8085,
    [string] $StoreRoot = 'D:\HousingDocuments',
    [string] $IndexFolder = 'D:\HousingData\index',
    [string] $ScanRoot = 'D:\Scans',
    [string] $ScannerGroup = 'CORP\HSG-Scanners',
    [string] $LogRoot = 'D:\Logs\Housing'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'HousingDeploy.psm1') -Force

$exe = Join-Path $InstallPath 'FabrikamDocumentService.exe'
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Stop-Service -Name $ServiceName
    $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
}

$project = Join-Path (Get-HousingSourceRoot) 'src\Fabrikam.Housing.DocumentService\Fabrikam.Housing.DocumentService.csproj'
& dotnet publish $project -c Release -o $InstallPath
if ($LASTEXITCODE -ne 0) { throw 'Publishing the document service failed.' }

$settings = [ordered]@{
    Urls = "http://*:$Port"
    ConnectionStrings = @{ Housing = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true;Application Name=Fabrikam Document Service" }
    Storage = [ordered]@{ Root = $StoreRoot; ThumbnailFolder = "$StoreRoot\_thumbs"; IndexFolder = $IndexFolder }
    Intake = [ordered]@{ WatchFolder = "$ScanRoot\Inbound"; ProcessedFolder = "$ScanRoot\Processed"; FailedFolder = "$ScanRoot\Failed" }
    Api = @{ Keys = @($ApiKeys | ForEach-Object { ConvertTo-PlainText $_ }) }
    Logging = @{ Folder = "$LogRoot\docs" }
}
Write-HousingSettings -Target $InstallPath -Settings $settings -Account $ServiceAccount
Grant-HousingFolder -Path @($StoreRoot, $IndexFolder, "$ScanRoot\Inbound", "$ScanRoot\Processed", "$ScanRoot\Failed", "$LogRoot\docs") -Account $ServiceAccount

if (-not $existing) {
    & $exe install --networkservice
    if ($LASTEXITCODE -ne 0) { throw 'Topshelf install failed.' }
}
# Run as the gMSA (no password: the account's password is managed by AD).
Grant-LogOnAsService -Account $ServiceAccount
& sc.exe config $ServiceName obj= $ServiceAccount password= '' | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sc.exe config failed for $ServiceName" }

if (-not (Get-SmbShare -Name 'Scans$' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'Scans$' -Path "$ScanRoot\Inbound" -ChangeAccess $ScannerGroup -FullAccess 'BUILTIN\Administrators' | Out-Null
}
Grant-HousingFolder -Path "$ScanRoot\Inbound" -Account $ScannerGroup

if (-not (Get-NetFirewallRule -Name 'FabrikamDocumentService' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'FabrikamDocumentService' -DisplayName 'Fabrikam Document Service' -Direction Inbound `
        -Protocol TCP -LocalPort $Port -Action Allow -Profile Domain | Out-Null
}

Start-Service -Name $ServiceName
Write-Host "Document service: http://$($env:COMPUTERNAME.ToLower()).corp.local:$Port/health"
