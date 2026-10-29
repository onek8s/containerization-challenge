<#
.SYNOPSIS
    Publishes the portal (rebates.corp.local) and the distributor API (rebates-api.corp.local) to IIS.
.DESCRIPTION
    Prerequisites: IIS with Windows Authentication, the ASP.NET Core 8 Hosting Bundle, the .NET 8 SDK on the build machine.
    Each application gets its own site and application pool under C:\inetpub\sites\<app>.
    Pools run as ApplicationPoolIdentity unless an account is given; with a domain account register the SPN
    (setspn -S HTTP/rebates.corp.local CORP\svc-rebates-web$) so browsers use Kerberos.
.EXAMPLE
    .\install-web.ps1 -SqlInstance rebates-sql.corp.local -CertificateThumbprint 3F2A... `
        -PortalPoolAccount 'CORP\svc-rebates-web$' -ApiPoolAccount 'CORP\svc-rebates-api$'
.EXAMPLE
    # Lab server, http only, local groups
    .\install-web.ps1 -SqlInstance 'host01\SQLEXPRESS' -AnalystGroups 'HOST01\TR-Analysts' -ApproverGroups 'HOST01\TR-Approvers' `
        -AdminGroups 'HOST01\TR-Admins'
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = 'C:\inetpub\sites',
    [string] $PortalHost = 'rebates.corp.local',
    [string] $ApiHost = 'rebates-api.corp.local',
    [string] $SqlInstance = 'rebates-sql.corp.local',
    [string] $DatabaseName = 'TradeRebates',
    [string] $FilesRoot = '\\rebates-files.corp.local\Rebates$',
    [string] $CertificateThumbprint,
    [string] $PortalPoolAccount,
    [securestring] $PortalPoolPassword,
    [string] $ApiPoolAccount,
    [securestring] $ApiPoolPassword,
    [string[]] $AnalystGroups = @('CORP\TR-Analysts'),
    [string[]] $ApproverGroups = @('CORP\TR-Approvers'),
    [string[]] $AdminGroups = @('CORP\TR-Admins')
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$ancm = Get-WebGlobalModule -Name 'AspNetCoreModuleV2' -ErrorAction SilentlyContinue
if (-not $ancm) { throw 'AspNetCoreModuleV2 is not registered. Install the ASP.NET Core 8 Hosting Bundle.' }

$src = Join-Path $PSScriptRoot '..\src'
$connection = "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=true;Encrypt=true;TrustServerCertificate=true"

function Publish-App([string] $Project, [string] $Target) {
    if (Test-Path (Join-Path $Target 'web.config')) {
        # Take the app offline while files are replaced.
        Set-Content -Path (Join-Path $Target 'app_offline.htm') -Value '<p>Trade Rebates is being updated.</p>'
        Start-Sleep -Seconds 3
    }
    & dotnet publish (Join-Path $src $Project) -c Release -o $Target
    if ($LASTEXITCODE -ne 0) { throw "Publishing $Project failed." }
    Remove-Item (Join-Path $Target 'app_offline.htm') -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $Target 'logs') -Force | Out-Null
}

function Set-Pool([string] $Name, [string] $Account, [securestring] $Password) {
    if (-not (Test-Path "IIS:\AppPools\$Name")) { New-WebAppPool -Name $Name | Out-Null }
    $pool = "IIS:\AppPools\$Name"
    Set-ItemProperty $pool -Name managedRuntimeVersion -Value ''
    Set-ItemProperty $pool -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty $pool -Name processModel.loadUserProfile -Value $true
    if ($Account) {
        $plain = ''
        if ($Password) { $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)) }
        Set-ItemProperty $pool -Name processModel -Value @{ userName = $Account; password = $plain; identityType = 3 }
    } else {
        Set-ItemProperty $pool -Name processModel.identityType -Value 'ApplicationPoolIdentity'
    }
}

function Set-Site([string] $Name, [string] $HostName, [string] $Path) {
    if (-not (Get-Website -Name $Name)) {
        New-Website -Name $Name -PhysicalPath $Path -ApplicationPool $Name -HostHeader $HostName -Port 80 | Out-Null
    } else {
        Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $Path
        Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $Name
    }
    if ($CertificateThumbprint) {
        if (-not (Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName)) {
            New-WebBinding -Name $Name -Protocol https -Port 443 -HostHeader $HostName -SslFlags 1
        }
        $binding = Get-WebBinding -Name $Name -Protocol https -HostHeader $HostName
        $binding.AddSslCertificate($CertificateThumbprint, 'My')
    }
}

# Portal
$portalPath = Join-Path $SiteRoot 'rebates'
Publish-App 'TradeRebates.Portal\TradeRebates.Portal.csproj' $portalPath
@{
    ConnectionStrings = @{ TradeRebates = "$connection;Application Name=TradeRebates.Portal" }
    Portal = @{ WorkbookUploadFolder = "$FilesRoot\Inbound\Uploads"; StatementRoot = "$FilesRoot\Statements" }
    Access = @{ Analysts = $AnalystGroups; Approvers = $ApproverGroups; Admins = $AdminGroups }
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $portalPath 'appsettings.Production.json') -Encoding UTF8
Set-Pool 'rebates' $PortalPoolAccount $PortalPoolPassword
Set-Site 'rebates' $PortalHost $portalPath

# Windows authentication is configured in the portal's web.config.
Set-WebConfiguration -Filter '/system.webServer/security/authentication/windowsAuthentication' -PSPath 'IIS:\' -Location 'rebates' -Metadata overrideMode -Value Allow
Set-WebConfiguration -Filter '/system.webServer/security/authentication/anonymousAuthentication' -PSPath 'IIS:\' -Location 'rebates' -Metadata overrideMode -Value Allow
if ($PortalPoolAccount) {
    Set-WebConfigurationProperty -Filter '/system.webServer/security/authentication/windowsAuthentication' -PSPath 'IIS:\' -Location 'rebates' -Name useAppPoolCredentials -Value $true
}

# API
$apiPath = Join-Path $SiteRoot 'rebates-api'
Publish-App 'TradeRebates.Api\TradeRebates.Api.csproj' $apiPath
@{ ConnectionStrings = @{ TradeRebates = "$connection;Application Name=TradeRebates.Api" } } |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $apiPath 'appsettings.Production.json') -Encoding UTF8
Set-Pool 'rebates-api' $ApiPoolAccount $ApiPoolPassword
Set-Site 'rebates-api' $ApiHost $apiPath
Set-WebConfiguration -Filter '/system.webServer/security/authentication/anonymousAuthentication' -PSPath 'IIS:\' -Location 'rebates-api' -Metadata overrideMode -Value Allow
Set-WebConfiguration -Filter '/system.webServer/security/authentication/windowsAuthentication' -PSPath 'IIS:\' -Location 'rebates-api' -Metadata overrideMode -Value Allow

Restart-WebAppPool -Name 'rebates'
Restart-WebAppPool -Name 'rebates-api'
Write-Host "Portal: http$(if ($CertificateThumbprint) { 's' })://$PortalHost/  API: http$(if ($CertificateThumbprint) { 's' })://$ApiHost/ping"
