<#
.SYNOPSIS
    Publishes the Expense Claims jobs host (scheduled interfaces) to IIS as expenses-jobs.corp.local.
.DESCRIPTION
    Prerequisites: IIS with Application Initialization, the ASP.NET Core 6 Hosting Bundle, the .NET 6 SDK.

    The scheduler runs inside the IIS worker process, so the application pool is AlwaysRunning, never
    idles out or recycles on a timer, and the site is preloaded. More than one server may run the jobs host:
    the Quartz job store in SQL Server is clustered and each job runs on one node at a time.

    The pool identity needs:
      - read access to the bank client certificate's private key (granted here when -BankCertificateThumbprint is given)
      - modify access to the HR, card and bank interface folders
      - a NAV user (Windows) with permission to insert lines into the EXPCLAIMS journal batch.
        With ApplicationPoolIdentity NAV sees the computer account CORP\<server>$; use -PoolAccount
        with a domain service account instead if NAV users are personal accounts only.
.EXAMPLE
    .\install-jobs.ps1 -SqlPassword (Read-Host -AsSecureString 'expenses_jobs') -BankCertificateThumbprint 9C1E... `
        -BankCustomerId 7731004 -DebtorIban FI7515723500050136 -DebtorBic OKOYFIHH -DebtorOrganisationId 2468135-7
    .\install-jobs.ps1 -SqlPassword $pw -BankMode Folder -PoolAccount 'CORP\svc-expenses-jobs' -PoolPassword $svc
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $HostName = 'expenses-jobs.corp.local',
    [string] $SqlInstance = 'expenses-sql.corp.local',
    [string] $DatabaseName = 'ExpenseClaims',
    [string] $SqlUser = 'expenses_jobs',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [string] $PoolAccount,
    [securestring] $PoolPassword,
    [string] $InterfaceRoot = 'D:\Interfaces',
    [string] $LogPath = 'D:\Logs\ExpenseClaims',
    [string] $ProxyAddress = 'http://proxy.corp.local:8080',
    [string] $SmtpHost = 'smtp.corp.local',
    [string] $PortalUrl = 'https://expenses.corp.local/',
    [ValidateSet('Gateway', 'Folder')] [string] $BankMode = 'Gateway',
    [string] $BankGatewayUrl = 'https://corporate-gateway.nordbank.example/api/v2/',
    [string] $BankCertificateThumbprint,
    [string] $BankCustomerId,
    [string] $DebtorIban,
    [string] $DebtorBic,
    [string] $DebtorOrganisationId,
    [string] $NavODataUrl = 'http://nav01.corp.local:7048/DynamicsNAV110/ODataV4/',
    [string] $NavCompany = 'Lamna Healthcare Oy',
    [string] $CertificateThumbprint,
    [switch] $TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if (-not (Get-WebGlobalModule -Name 'AspNetCoreModuleV2' -ErrorAction SilentlyContinue)) {
    throw 'AspNetCoreModuleV2 is not registered. Install the ASP.NET Core 6 Hosting Bundle.'
}
if (-not (Get-WindowsFeature Web-AppInit).Installed) {
    throw 'IIS Application Initialization is not installed: Install-WindowsFeature Web-AppInit'
}
if ($BankMode -eq 'Gateway' -and -not $BankCertificateThumbprint) {
    throw 'BankCertificateThumbprint is required with -BankMode Gateway.'
}

$pool = 'expenses-jobs'
$target = Join-Path $SiteRoot $pool
$project = Join-Path $PSScriptRoot '..\src\ExpenseClaims.Jobs\ExpenseClaims.Jobs.csproj'

if (Test-Path (Join-Path $target 'web.config')) {
    Set-Content -Path (Join-Path $target 'app_offline.htm') -Value 'Updating'
    Start-Sleep -Seconds 10
}
& dotnet publish $project -c Release -o $target
if ($LASTEXITCODE -ne 0) { throw 'Publishing the jobs host failed.' }

$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
$connection = "Server=$SqlInstance;Database=$DatabaseName;User Id=$SqlUser;Password=$plain;Encrypt=True;TrustServerCertificate=$($TrustServerCertificate.IsPresent);Application Name=Expense Claims Jobs"

$folders = [ordered]@{
    Hr = Join-Path $InterfaceRoot 'HR\Inbound'
    Cards = Join-Path $InterfaceRoot 'Cards\Inbound'
    Bank = Join-Path $InterfaceRoot 'Bank\Outbound'
}

[ordered]@{
    ConnectionStrings = @{ ExpenseClaims = $connection }
    Proxy = @{ Address = $ProxyAddress }
    HrImport = @{ InboundFolder = $folders.Hr }
    CardImport = @{ InboundFolder = $folders.Cards }
    Smtp = @{ Host = $SmtpHost; PortalUrl = $PortalUrl }
    Bank = [ordered]@{
        Mode = $BankMode
        GatewayUrl = $BankGatewayUrl
        ClientCertificateThumbprint = "$BankCertificateThumbprint"
        CustomerId = "$BankCustomerId"
        OutboundFolder = $folders.Bank
        DebtorIban = "$DebtorIban"
        DebtorBic = "$DebtorBic"
        DebtorOrganisationId = "$DebtorOrganisationId"
    }
    Erp = @{ ODataUrl = $NavODataUrl; Company = $NavCompany }
    Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = (Join-Path $LogPath 'jobs-.log') } }) }
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $target 'appsettings.Production.json') -Encoding UTF8
Remove-Item (Join-Path $target 'app_offline.htm') -ErrorAction SilentlyContinue

if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
$poolPath = "IIS:\AppPools\$pool"
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ''
Set-ItemProperty $poolPath -Name startMode -Value AlwaysRunning
Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true
Set-ItemProperty $poolPath -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Clear-ItemProperty $poolPath -Name recycling.periodicRestart.schedule
Set-ItemProperty $poolPath -Name failure.rapidFailProtection -Value $false
if ($PoolAccount) {
    $poolPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($PoolPassword))
    Set-ItemProperty $poolPath -Name processModel -Value @{ userName = $PoolAccount; password = $poolPlain; identityType = 3 }
    $identity = $PoolAccount
} else {
    Set-ItemProperty $poolPath -Name processModel.identityType -Value ApplicationPoolIdentity
    $identity = "IIS AppPool\$pool"
}

if (-not (Get-Website -Name $pool)) {
    New-Website -Name $pool -PhysicalPath $target -ApplicationPool $pool -HostHeader $HostName -Port 80 | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$pool" -Name physicalPath -Value $target
}
Set-ItemProperty "IIS:\Sites\$pool" -Name applicationDefaults.preloadEnabled -Value $true
if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $pool -Protocol https -HostHeader $HostName)) {
        New-WebBinding -Name $pool -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    }
    (Get-WebBinding -Name $pool -Protocol https -HostHeader $HostName).AddSslCertificate($CertificateThumbprint, 'My')
}

foreach ($path in @($folders.Values) + @($LogPath, (Join-Path $target 'logs'))) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    & icacls $path /grant "${identity}:(OI)(CI)M" /T /Q | Out-Null
}
& icacls (Join-Path $target 'appsettings.Production.json') /inheritance:r /grant:r 'BUILTIN\Administrators:F' "${identity}:R" | Out-Null

if ($BankCertificateThumbprint) {
    & (Join-Path $PSScriptRoot 'grant-certificate-key.ps1') -Thumbprint $BankCertificateThumbprint -Identity $identity
}

Restart-WebAppPool -Name $pool
Write-Host "Jobs host: http://$HostName/ (job status page; run a job from this server with POST /jobs/<name>/run)"
