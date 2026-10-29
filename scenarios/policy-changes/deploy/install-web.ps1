<#
.SYNOPSIS
    Creates the IIS sites for the Policy Changes portal and partner API.
.DESCRIPTION
    Prerequisites: IIS with Windows Authentication, ASP.NET Core 8 Hosting Bundle.
    Publish first:
      dotnet publish src\PolicyChanges.Portal -c Release -o publish\portal
      dotnet publish src\PolicyChanges.Api -c Release -o publish\api
.EXAMPLE
    .\install-web.ps1 -PublishRoot ..\publish -DomainName corp.local -AppPoolAccount 'CORP\svc-policychanges'
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $DomainName = 'corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $PortalHost = 'changes',
    [string] $ApiHost = 'changes-api',
    [Parameter(Mandatory)] [string] $AppPoolAccount,
    [string] $EvidenceShare = '\\files01.corp.local\PolicyChanges$\Evidence',
    [int] $Port = 80
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$credential = Get-Credential -UserName $AppPoolAccount -Message 'Application pool account'

$sites = @(
    @{ Name = $PortalHost; Source = 'portal'; HostName = "$PortalHost.$DomainName"; Windows = $true },
    @{ Name = $ApiHost; Source = 'api'; HostName = "$ApiHost.$DomainName"; Windows = $false }
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
        #   setspn -S HTTP/<PortalHost>.<DomainName> <AppPoolAccount>
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
            -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true
    }

    Start-WebAppPool -Name $s.Name
    Write-Host "http://$($s.HostName)/ -> $path"
}

Write-Host "The portal shows screenshots from $EvidenceShare; $AppPoolAccount needs read access there."
Write-Host "Register the SPN for the portal if it is not registered yet:"
Write-Host "  setspn -S HTTP/$PortalHost.$DomainName $AppPoolAccount"
