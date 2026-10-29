<#
.SYNOPSIS
    Creates the IIS sites for the Freight Rates API and admin portal.
.DESCRIPTION
    Prerequisites: IIS with Windows Authentication, ASP.NET Core 8 Hosting Bundle.
    Publish first:
      dotnet publish src\FreightRates.Api -c Release -o publish\api
      dotnet publish src\FreightRates.Admin -c Release -o publish\admin
.EXAMPLE
    .\install-web.ps1 -PublishRoot ..\publish -DomainName corp.local -AppPoolAccount 'CORP\svc-freightrates'
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $DomainName = 'corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $ApiHost = 'rates',
    [string] $AdminHost = 'rates-admin',
    [Parameter(Mandatory)] [string] $AppPoolAccount,
    [int] $Port = 80
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$credential = Get-Credential -UserName $AppPoolAccount -Message 'Application pool account'

$sites = @(
    @{ Name = 'freightrates-api'; Source = 'api'; HostName = "$ApiHost.$DomainName"; Windows = $false },
    @{ Name = 'freightrates-admin'; Source = 'admin'; HostName = "$AdminHost.$DomainName"; Windows = $true }
)

foreach ($s in $sites) {
    $path = Join-Path $SitesRoot $s.Name
    New-Item -ItemType Directory -Force -Path $path | Out-Null

    if (-not (Test-Path "IIS:\AppPools\$($s.Name)")) {
        New-WebAppPool -Name $s.Name | Out-Null
    }
    Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$($s.Name)" -Name processModel -Value @{
        identityType = 3
        userName = $credential.UserName
        password = $credential.GetNetworkCredential().Password
    }
    Stop-WebAppPool -Name $s.Name -ErrorAction SilentlyContinue

    Copy-Item (Join-Path $PublishRoot "$($s.Source)\*") $path -Recurse -Force
    icacls $path /grant "$($AppPoolAccount):(OI)(CI)RX" | Out-Null

    if (-not (Get-Website -Name $s.Name)) {
        New-Website -Name $s.Name -PhysicalPath $path -ApplicationPool $s.Name -HostHeader $s.HostName -Port $Port | Out-Null
    }

    if ($s.Windows) {
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
            -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $true
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
            -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $false
        # Kerberos with the application pool account; register the HTTP SPN for that account:
        #   setspn -S HTTP/<AdminHost>.<DomainName> <AppPoolAccount>
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
            -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true
    }

    Start-WebAppPool -Name $s.Name
    Write-Host "http://$($s.HostName)/ -> $path"
}

Write-Host "Register the SPN for the admin portal if it is not registered yet:"
Write-Host "  setspn -S HTTP/$AdminHost.$DomainName $AppPoolAccount"
