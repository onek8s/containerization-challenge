<#
.SYNOPSIS
    Creates the IIS sites for the Furnace Records portal and MES API.
.DESCRIPTION
    Prerequisites: IIS with Windows Authentication, ASP.NET Core 8 Hosting Bundle.
    Publish first:
      dotnet publish src\FurnaceRecords.Portal -c Release -o publish\portal
      dotnet publish src\FurnaceRecords.Api -c Release -o publish\api
.EXAMPLE
    .\install-web.ps1 -PublishRoot ..\publish -AppPoolAccount 'CORP\svc-furnace-web'
#>
param(
    [Parameter(Mandatory)] [string] $PublishRoot,
    [string] $DomainName = 'corp.local',
    [string] $SitesRoot = 'C:\inetpub\sites',
    [string] $PortalHost = 'furnace',
    [string] $ApiHost = 'furnace-api',
    [string] $DaqHost = 'daq01',
    [string] $SqlServer = 'sql01',
    [Parameter(Mandatory)] [string] $AppPoolAccount,
    [string] $OperatorsGroup = 'CORP\FR-Operators',
    [string] $QualityGroup = 'CORP\FR-Quality',
    [string] $AdminsGroup = 'CORP\FR-Admins',
    [int] $Port = 80
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$credential = Get-Credential -UserName $AppPoolAccount -Message 'Application pool account'
$connection = "Server=$SqlServer.$DomainName;Database=FurnaceRecords;Integrated Security=true;TrustServerCertificate=true"

$sites = @(
    @{ Name = 'furnace'; Source = 'portal'; HostName = "$PortalHost.$DomainName"; Windows = $true },
    @{ Name = 'furnace-api'; Source = 'api'; HostName = "$ApiHost.$DomainName"; Windows = $false }
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
    Start-Sleep -Seconds 2

    Copy-Item (Join-Path $PublishRoot "$($s.Source)\*") $path -Recurse -Force
    icacls $path /grant "$($AppPoolAccount):(OI)(CI)RX" | Out-Null

    if (-not (Get-Website -Name $s.Name)) {
        New-Website -Name $s.Name -PhysicalPath $path -ApplicationPool $s.Name -HostHeader $s.HostName -Port $Port | Out-Null
    }

    $windows = [bool]$s.Windows
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
        -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name enabled -Value $windows
    Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
        -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value (-not $windows)
    if ($windows) {
        Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location $s.Name `
            -Filter 'system.webServer/security/authentication/windowsAuthentication' -Name useAppPoolCredentials -Value $true
    }

    # Settings as environment variables of the site
    $vars = @{
        'ConnectionStrings__FurnaceRecords' = $connection
    }
    if ($s.Source -eq 'portal') {
        $vars['Portal__DaqAddress'] = "http://$DaqHost.${DomainName}:5200"
        $vars['Portal__OperatorsGroup'] = $OperatorsGroup
        $vars['Portal__QualityGroup'] = $QualityGroup
        $vars['Portal__AdminsGroup'] = $AdminsGroup
    }
    $filter = "system.webServer/aspNetCore/environmentVariables"
    foreach ($name in $vars.Keys) {
        Remove-WebConfigurationProperty -PSPath "IIS:\Sites\$($s.Name)" -Filter $filter -Name '.' -AtElement @{ name = $name } -ErrorAction SilentlyContinue
        Add-WebConfigurationProperty -PSPath "IIS:\Sites\$($s.Name)" -Filter $filter -Name '.' -Value @{ name = $name; value = $vars[$name] }
    }

    Start-WebAppPool -Name $s.Name
    Write-Host "http://$($s.HostName)/ -> $path"
}

Write-Host "Register the SPN for the portal if it is not registered yet:"
Write-Host "  setspn -S HTTP/$PortalHost.$DomainName $AppPoolAccount"
Write-Host "Issue an API key for MES:"
Write-Host "  cd $(Join-Path $SitesRoot 'furnace-api'); `$env:ConnectionStrings__FurnaceRecords='$connection'; .\FurnaceRecords.Api.exe issue-key MES01"
