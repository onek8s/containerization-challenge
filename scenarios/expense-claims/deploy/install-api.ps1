<#
.SYNOPSIS
    Publishes the Expense Claims Web API to IIS as expenses-api.corp.local.
.DESCRIPTION
    Prerequisites: IIS, the ASP.NET Core 6 Hosting Bundle, the .NET 6 SDK on the build machine.
    The application pool runs as ApplicationPoolIdentity; the database is reached with the SQL login
    expenses_app. Receipts are stored under -ReceiptsPath and logs under -LogPath; both get modify rights
    for the pool identity.
.EXAMPLE
    .\install-api.ps1 -SqlPassword (Read-Host -AsSecureString 'expenses_app') -CertificateThumbprint 3F2A...
    .\install-api.ps1 -SqlPassword $pw -AuthMode Development -SpaOrigin http://expenses.corp.local
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $HostName = 'expenses-api.corp.local',
    [string] $SpaOrigin = 'https://expenses.corp.local',
    [string] $SqlInstance = 'expenses-sql.corp.local',
    [string] $DatabaseName = 'ExpenseClaims',
    [string] $SqlUser = 'expenses_app',
    [Parameter(Mandatory)] [securestring] $SqlPassword,
    [ValidateSet('Adfs', 'Development')] [string] $AuthMode = 'Adfs',
    [string] $AdfsAuthority = 'https://adfs.corp.local/adfs',
    [string] $ApiIdentifier = 'https://expenses-api.corp.local',
    [string] $ReceiptsPath = 'D:\ExpenseReceipts',
    [string] $LogPath = 'D:\Logs\ExpenseClaims',
    [string] $FallbackApprover = '100001',
    [string] $CertificateThumbprint,
    [switch] $TrustServerCertificate
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

if (-not (Get-WebGlobalModule -Name 'AspNetCoreModuleV2' -ErrorAction SilentlyContinue)) {
    throw 'AspNetCoreModuleV2 is not registered. Install the ASP.NET Core 6 Hosting Bundle.'
}

$pool = 'expenses-api'
$target = Join-Path $SiteRoot $pool
$project = Join-Path $PSScriptRoot '..\src\ExpenseClaims.Api\ExpenseClaims.Api.csproj'

if (Test-Path (Join-Path $target 'web.config')) {
    Set-Content -Path (Join-Path $target 'app_offline.htm') -Value '<p>Expense Claims is being updated.</p>'
    Start-Sleep -Seconds 3
}
& dotnet publish $project -c Release -o $target
if ($LASTEXITCODE -ne 0) { throw 'Publishing the API failed.' }

$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword))
$connection = "Server=$SqlInstance;Database=$DatabaseName;User Id=$SqlUser;Password=$plain;Encrypt=True;TrustServerCertificate=$($TrustServerCertificate.IsPresent);Application Name=Expense Claims API"

$settings = [ordered]@{
    ConnectionStrings = @{ ExpenseClaims = $connection }
    Authentication = [ordered]@{
        Mode = $AuthMode
        Adfs = [ordered]@{
            Authority = $AdfsAuthority
            Audience = $ApiIdentifier
            ValidIssuers = @(
                ($AdfsAuthority -replace '^https://', 'http://').TrimEnd('/') + '/services/trust',
                $AdfsAuthority.TrimEnd('/'))
        }
    }
    Cors = @{ Origins = @($SpaOrigin) }
    Receipts = @{ RootPath = $ReceiptsPath }
    Approval = @{ FallbackApprover = $FallbackApprover }
    Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = (Join-Path $LogPath 'api-.log') } }) }
}
if ($AuthMode -eq 'Development') {
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $settings.Authentication.Development = @{ SigningKey = [Convert]::ToBase64String($bytes) }
    Write-Warning 'Development sign-in: anyone who can reach the API can sign in as any employee. Use only in a test environment.'
}
$settings | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $target 'appsettings.Production.json') -Encoding UTF8
Remove-Item (Join-Path $target 'app_offline.htm') -ErrorAction SilentlyContinue

if (-not (Test-Path "IIS:\AppPools\$pool")) { New-WebAppPool -Name $pool | Out-Null }
Set-ItemProperty "IIS:\AppPools\$pool" -Name managedRuntimeVersion -Value ''
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.identityType -Value ApplicationPoolIdentity
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.loadUserProfile -Value $true

if (-not (Get-Website -Name $pool)) {
    New-Website -Name $pool -PhysicalPath $target -ApplicationPool $pool -HostHeader $HostName -Port 80 | Out-Null
} else {
    Set-ItemProperty "IIS:\Sites\$pool" -Name physicalPath -Value $target
}
if ($CertificateThumbprint) {
    if (-not (Get-WebBinding -Name $pool -Protocol https -HostHeader $HostName)) {
        New-WebBinding -Name $pool -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
    }
    (Get-WebBinding -Name $pool -Protocol https -HostHeader $HostName).AddSslCertificate($CertificateThumbprint, 'My')
}

$identity = "IIS AppPool\$pool"
foreach ($path in @($ReceiptsPath, $LogPath, (Join-Path $target 'logs'))) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    & icacls $path /grant "${identity}:(OI)(CI)M" /T /Q | Out-Null
}
# appsettings.Production.json holds the SQL password: only administrators and the pool may read it.
& icacls (Join-Path $target 'appsettings.Production.json') /inheritance:r /grant:r 'BUILTIN\Administrators:F' "${identity}:R" | Out-Null

Restart-WebAppPool -Name $pool
$scheme = if ($CertificateThumbprint) { 'https' } else { 'http' }
Write-Host "API: ${scheme}://$HostName/health (sign-in: $AuthMode)"
