<#
.SYNOPSIS
    Installs the statement worker and configures the Service Broker External Activator service to start it.
.DESCRIPTION
    Prerequisites on the server:
      - Microsoft SQL Server Service Broker External Activator (service SSBExternalActivator, from the SQL Server feature pack)
      - .NET 8 runtime (x64)
    The activator runs the worker under its own service account; that account is the worker's database login
    (db\11-security.sql, ActivatorAccount) and needs Modify on the Statements folder.
.EXAMPLE
    .\install-activator.ps1 -SqlInstance rebates-sql.corp.local -ServiceAccount 'CORP\svc-rebates-ea$'
.EXAMPLE
    $cred = Get-Credential 'HOST01\svc-rebates-ea'
    .\install-activator.ps1 -SqlInstance 'host01\SQLEXPRESS' -ServiceAccount $cred.UserName -ServicePassword $cred.Password `
        -StatementRoot 'D:\Shares\Rebates\Statements'
#>
[CmdletBinding()]
param(
    [string] $SqlInstance = 'rebates-sql.corp.local',
    [string] $DatabaseName = 'TradeRebates',
    [string] $WorkerPath = 'D:\TradeRebates\StatementWorker',
    [string] $StatementRoot = '\\rebates-files.corp.local\Rebates$\Statements',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [securestring] $ServicePassword,
    [int] $MaxWorkers = 2,
    [string] $ActivatorConfigFolder = "$env:ProgramFiles\Service Broker\External Activator\Config"
)

$ErrorActionPreference = 'Stop'
$serviceName = 'SSBExternalActivator'

if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) {
    throw "Service $serviceName is not installed. Install the SQL Server Service Broker External Activator first."
}

# Worker
$project = Join-Path $PSScriptRoot '..\src\TradeRebates.StatementWorker\TradeRebates.StatementWorker.csproj'
& dotnet publish $project -c Release -r win-x64 --self-contained false -o $WorkerPath
if ($LASTEXITCODE -ne 0) { throw 'Publishing the statement worker failed.' }

$settingsFile = Join-Path $WorkerPath 'appsettings.json'
$settings = Get-Content $settingsFile -Raw | ConvertFrom-Json
$settings.ConnectionStrings.TradeRebates = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Application Name=TradeRebates.StatementWorker"
$settings.Statements.Root = $StatementRoot
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsFile -Encoding UTF8

# The activator matches notifications on @@SERVERNAME, not on the DNS name used to connect.
$serverName = (& sqlcmd.exe -S $SqlInstance -E -C -h -1 -W -Q 'SET NOCOUNT ON; SELECT @@SERVERNAME;' | Where-Object { $_ }) | Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or -not $serverName) { throw "Could not read @@SERVERNAME from $SqlInstance." }

$template = Get-Content (Join-Path $PSScriptRoot 'activator\EAService.config.template') -Raw
$config = $template.Replace('{SqlInstance}', $SqlInstance).Replace('{DatabaseName}', $DatabaseName).Replace('{ServerName}', $serverName.Trim()).
    Replace('{WorkerPath}', $WorkerPath).Replace('{MaxWorkers}', [string]$MaxWorkers)

$configFile = Join-Path $ActivatorConfigFolder 'EAService.config'
if (Test-Path $configFile) {
    Copy-Item $configFile "$configFile.$(Get-Date -Format yyyyMMddHHmmss).bak"
}
Set-Content -Path $configFile -Value $config -Encoding UTF8

# Run the activator as the worker account (a group managed service account has no password).
$plain = $null
if ($ServicePassword) {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($ServicePassword))
}
$result = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" |
    Invoke-CimMethod -MethodName Change -Arguments @{ StartName = $ServiceAccount; StartPassword = $plain }
if ($result.ReturnValue -ne 0) { throw "Changing the $serviceName account failed ($($result.ReturnValue))." }

& (Join-Path $PSScriptRoot 'grant-logon-right.ps1') -Account $ServiceAccount -Right SeServiceLogonRight

& icacls.exe $WorkerPath /grant "${ServiceAccount}:(OI)(CI)RX" /Q | Out-Null
$activatorLog = Join-Path (Split-Path $ActivatorConfigFolder) 'Log'
if (Test-Path $activatorLog) { & icacls.exe $activatorLog /grant "${ServiceAccount}:(OI)(CI)M" /Q | Out-Null }

Restart-Service -Name $serviceName
Write-Host "External Activator configured for $serverName/$DatabaseName, worker in $WorkerPath."
