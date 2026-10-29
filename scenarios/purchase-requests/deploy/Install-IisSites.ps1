<#
.SYNOPSIS
    Creates the IIS application pools and sites for the purchasing portal and API and copies the published files.
    Run as administrator on the web server(s). Use -Sites to install only one of them on a server.
.EXAMPLE
    .\Install-IisSites.ps1 -PublishPath ..\publish -ZoneName corp.local `
        -WebPoolAccount CORP\svc-purch-web -ApiPoolAccount CORP\svc-purch-api
#>
param(
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$SiteRoot = 'C:\inetpub\sites',
    [string[]]$Sites = @('Purchasing.Web', 'Purchasing.Api'),
    [string]$WebPoolAccount,
    [string]$ApiPoolAccount,
    [string]$LogFolder = 'D:\Logs\Purchasing'
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$definitions = @{
    'Purchasing.Web' = @{ Site = 'Purchasing';    Pool = 'Purchasing';    HostName = "purchasing.$ZoneName";     Account = $WebPoolAccount }
    'Purchasing.Api' = @{ Site = 'PurchasingApi'; Pool = 'PurchasingApi'; HostName = "purchasing-api.$ZoneName"; Account = $ApiPoolAccount }
}

New-Item -ItemType Directory -Force -Path $LogFolder | Out-Null

foreach ($name in $Sites) {
    $d = $definitions[$name]
    $physical = Join-Path $SiteRoot $d.Site
    $source = Join-Path $PublishPath $name
    if (-not (Test-Path $source)) { throw "$source not found. Run Publish.ps1 first." }

    New-Item -ItemType Directory -Force -Path $physical | Out-Null
    robocopy $source $physical /MIR /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copying $name failed." }

    $poolPath = "IIS:\AppPools\$($d.Pool)"
    if (-not (Test-Path $poolPath)) { New-WebAppPool -Name $d.Pool | Out-Null }
    Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty $poolPath -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty $poolPath -Name processModel.loadUserProfile -Value $true

    if ($d.Account) {
        $credential = Get-Credential -UserName $d.Account -Message "Password for the $($d.Pool) application pool account"
        Set-ItemProperty $poolPath -Name processModel -Value @{
            userName = $credential.UserName
            password = $credential.GetNetworkCredential().Password
            identityType = 3
        }
    }

    if (-not (Get-Website -Name $d.Site)) {
        New-Website -Name $d.Site -PhysicalPath $physical -ApplicationPool $d.Pool -HostHeader $d.HostName -Port 80 | Out-Null
    }
    else {
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name physicalPath -Value $physical
        Set-ItemProperty "IIS:\Sites\$($d.Site)" -Name applicationPool -Value $d.Pool
    }

    # Windows Authentication only. The pool credentials are used for kernel mode authentication so that
    # Kerberos tickets for the host name SPN can be decrypted by the service account.
    $filter = 'system.webServer/security/authentication'
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter "$filter/anonymousAuthentication" -Name enabled -Value $false
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter "$filter/windowsAuthentication" -Name enabled -Value $true
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $d.Site -Filter "$filter/windowsAuthentication" -Name useAppPoolCredentials -Value $true

    $account = if ($d.Account) { $d.Account } else { "IIS AppPool\$($d.Pool)" }
    icacls $LogFolder /grant "${account}:(OI)(CI)M" | Out-Null

    Write-Host "$name installed: http://$($d.HostName)/ -> $physical (pool $($d.Pool), $account)"
}

Write-Host ''
Write-Host 'Service principal names and delegation (domain installations):'
Write-Host "  setspn -S HTTP/purchasing.$ZoneName $WebPoolAccount"
Write-Host "  setspn -S HTTP/purchasing-api.$ZoneName $ApiPoolAccount"
Write-Host "  Allow $WebPoolAccount to delegate to HTTP/purchasing-api.$ZoneName (Kerberos constrained delegation)."
