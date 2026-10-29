<#
.SYNOPSIS
    Creates the IIS sites of Broker Quotes and copies the published applications.
.DESCRIPTION
    Sites (one application pool each, site folders under -SiteRoot):

        login.<zone>      Humongous.Identity       sign-in (OpenID Connect provider)
        brokers.<zone>    Humongous.BrokerPortal   broker portal (React app and portal host)
        workbench.<zone>  Humongous.Workbench      underwriting workbench (Blazor Server, WebSockets)
        quote-api.<zone>  Humongous.Quoting.Api    quote API (32-bit application pool, RateCore in process)

    Requires IIS with WebSockets, the ASP.NET Core 8 Hosting Bundle (x64 and x86) and the
    .NET 8 Windows Desktop Runtime x86 (needed by the quote API on this server).
.EXAMPLE
    .\Install-WebSites.ps1 -PublishRoot D:\Drops\BrokerQuotes\5.8.0 -CertificateThumbprint 3F1A... -SqlPasswords $pw -ClientSecrets $secrets
.EXAMPLE
    $lab = Import-PowerShellDataFile .\lab.psd1
    .\Install-WebSites.ps1 -PublishRoot C:\Drops\bq -CertificateThumbprint $thumb -RatingProvider $lab.RatingProvider ...
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [Parameter(Mandatory)] [string] $CertificateThumbprint,
    [Parameter(Mandatory)] [hashtable] $SqlPasswords,       # @{ Quoting = '...'; Identity = '...' }
    [Parameter(Mandatory)] [hashtable] $ClientSecrets,      # @{ BrokerPortal = '...'; Workbench = '...' }
    [Parameter(Mandatory)] [string] $MqPassword,
    [string] $ZoneName = 'corp.local',
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $SqlServer = 'sql01',
    [string] $MqServer = 'mq01',
    [ValidateSet('RateCore', 'Simulator')] [string] $RatingProvider = 'RateCore',
    [string] $RateCoreTablesPath = 'D:\RateCore\Tables',
    [string] $SigningCertificateThumbprint,
    [string] $EncryptionCertificateThumbprint,
    [string] $LogRoot = 'D:\Logs\Humongous'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Common.psm1')
Import-Module WebAdministration
Assert-Administrator

if ($RatingProvider -eq 'RateCore' -and -not (Test-RateCoreRegistered)) {
    throw 'RateCore is not registered on this server. Run Install-RateCore.ps1 first, or use -RatingProvider Simulator on a training server.'
}

$sql = "$SqlServer.$ZoneName"
$login = "https://login.$ZoneName/"
$api = "https://quote-api.$ZoneName/"

$sites = @(
    @{ Name = 'login'; Folder = 'login'; Is32Bit = $false; Settings = @{
        ConnectionStrings = @{ Identity = "Server=$sql;Database=HumongousIdentity;User Id=hq_identity;Password=$($SqlPasswords.Identity);Encrypt=True" }
        Identity = @{ Issuer = $login; SigningCertificateThumbprint = $SigningCertificateThumbprint; EncryptionCertificateThumbprint = $EncryptionCertificateThumbprint }
        Clients = @(
            @{ ClientId = 'broker-portal'; DisplayName = 'Broker portal'; ClientSecret = $ClientSecrets.BrokerPortal
               RedirectUris = @("https://brokers.$ZoneName/signin-oidc"); PostLogoutRedirectUris = @("https://brokers.$ZoneName/signout-callback-oidc") },
            @{ ClientId = 'workbench'; DisplayName = 'Underwriting workbench'; ClientSecret = $ClientSecrets.Workbench
               RedirectUris = @("https://workbench.$ZoneName/signin-oidc"); PostLogoutRedirectUris = @("https://workbench.$ZoneName/signout-callback-oidc") })
        Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\Identity\identity-.log"; rollingInterval = 'Day' } }) }
    } },
    @{ Name = 'brokers'; Folder = 'brokers'; Is32Bit = $false; Settings = @{
        Oidc = @{ Authority = $login; ClientSecret = $ClientSecrets.BrokerPortal }
        ReverseProxy = @{ Clusters = @{ 'quote-api' = @{ Destinations = @{ primary = @{ Address = $api } } } } }
        Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\BrokerPortal\portal-.log"; rollingInterval = 'Day' } }) }
    } },
    @{ Name = 'workbench'; Folder = 'workbench'; Is32Bit = $false; Settings = @{
        Oidc = @{ Authority = $login; ClientSecret = $ClientSecrets.Workbench }
        QuoteApi = @{ BaseAddress = $api }
        Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\Workbench\workbench-.log"; rollingInterval = 'Day' } }) }
    } },
    @{ Name = 'quote-api'; Folder = 'quote-api'; Is32Bit = $true; Settings = @{
        ConnectionStrings = @{ Quoting = "Server=$sql;Database=HumongousQuoting;User Id=hq_quoting;Password=$($SqlPasswords.Quoting);Encrypt=True;Application Name=Broker Quotes API" }
        Rating = @{ Provider = $RatingProvider; TablesPath = $RateCoreTablesPath }
        Bus = @{ Host = "$MqServer.$ZoneName"; Password = $MqPassword }
        Authentication = @{ Authority = $login }
        Serilog = @{ WriteTo = @(@{ Name = 'File'; Args = @{ path = "$LogRoot\QuoteApi\quote-api-.log"; rollingInterval = 'Day' } }) }
    } }
)

foreach ($site in $sites) {
    $name = $site.Name
    $hostName = "$name.$ZoneName"
    $path = Join-Path $SiteRoot $site.Folder
    $source = Join-Path $PublishRoot $site.Folder

    if (-not (Test-Path $source)) { throw "Published files for $name not found in $source." }

    if (-not (Test-Path "IIS:\AppPools\$name")) { New-WebAppPool -Name $name | Out-Null }
    Set-ItemProperty "IIS:\AppPools\$name" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$name" -Name enable32BitAppOnWin64 -Value $site.Is32Bit
    Set-ItemProperty "IIS:\AppPools\$name" -Name processModel.loadUserProfile -Value $true
    Set-ItemProperty "IIS:\AppPools\$name" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty "IIS:\AppPools\$name" -Name startMode -Value 'AlwaysRunning'
    # Quotes in rating are re-queued at start; recycle at night, not every 29 hours.
    Set-ItemProperty "IIS:\AppPools\$name" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
    Clear-ItemProperty "IIS:\AppPools\$name" -Name recycling.periodicRestart.schedule
    New-ItemProperty "IIS:\AppPools\$name" -Name recycling.periodicRestart.schedule -Value @{ value = '03:30:00' } | Out-Null

    if (Test-Path "IIS:\Sites\$name") { Stop-WebSite -Name $name }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    Copy-Item (Join-Path $source '*') $path -Recurse -Force
    Write-ProductionSettings -Path $path -Settings $site.Settings
    Grant-FolderAccess -Path $path -Identity "IIS AppPool\$name" -Rights ReadAndExecute

    if (-not (Test-Path "IIS:\Sites\$name")) {
        New-WebSite -Name $name -PhysicalPath $path -ApplicationPool $name -HostHeader $hostName -Port 443 -Ssl -SslFlags 1 | Out-Null
        $binding = Get-WebBinding -Name $name -Protocol https
        $binding.AddSslCertificate($CertificateThumbprint, 'My')
        New-WebBinding -Name $name -Protocol http -Port 80 -HostHeader $hostName
    }
    Set-ItemProperty "IIS:\Sites\$name" -Name applicationDefaults.preloadEnabled -Value $true
    Start-WebSite -Name $name
    Write-Host "https://$hostName -> $path (pool $name, 32-bit: $($site.Is32Bit))"
}

New-Item -ItemType Directory -Path $LogRoot -Force | Out-Null
foreach ($name in 'login', 'brokers', 'workbench', 'quote-api') {
    Grant-FolderAccess -Path $LogRoot -Identity "IIS AppPool\$name" -Rights Modify
}
if ($RatingProvider -eq 'RateCore') {
    Grant-FolderAccess -Path $RateCoreTablesPath -Identity 'IIS AppPool\quote-api' -Rights ReadAndExecute
}
