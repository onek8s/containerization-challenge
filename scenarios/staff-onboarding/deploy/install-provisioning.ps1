<#
.SYNOPSIS
    Installs the Staff Onboarding provisioning Windows service on idm01.corp.local.
.DESCRIPTION
    Run as administrator on idm01 (Windows Server 2019 or later, ASP.NET Core 8 runtime, domain joined,
    allowed to retrieve the gMSA password). Build output from
    dotnet publish src\StaffOnboarding.Provisioning -c Release -o C:\Install\Provisioning
#>
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $InstallPath = 'C:\Services\StaffOnboarding\Provisioning',
    [string] $Domain = 'CORP',
    [string] $ServiceAccount = 'svc-idm',
    [string] $HostName = 'idm01.corp.local',
    [int] $Port = 8090,
    [string] $WorkflowServerAddress = 'onboarding-flow.corp.local'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$account = "$Domain\$ServiceAccount$"
$serviceName = 'StaffOnboardingIdm'

Install-Gmsa $ServiceAccount
Grant-ServiceLogonRight $account
Ensure-EventSource 'Staff Onboarding Provisioning'

if (Get-Service $serviceName -ErrorAction SilentlyContinue) { Stop-Service $serviceName -Force }
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
robocopy $SourcePath $InstallPath /MIR /XF 'appsettings.Production.json' /NFL /NDL /NJH /NJS | Out-Null

netsh http delete urlacl url="http://${HostName}:$Port/" 2>$null | Out-Null
netsh http add urlacl url="http://${HostName}:$Port/" user="$account" | Out-Host
$workflow = (Resolve-DnsName $WorkflowServerAddress -Type A).IPAddress
if (-not (Get-NetFirewallRule -Name 'StaffOnboardingIdm' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'StaffOnboardingIdm' -DisplayName 'Staff Onboarding provisioning' -Direction Inbound `
        -Protocol TCP -LocalPort $Port -RemoteAddress $workflow -Action Allow | Out-Null
}

# LDAP signing and sealing are required by the domain controllers; SMB signing for the DFS home folders.
Set-SmbClientConfiguration -RequireSecuritySignature $true -Force

Install-WindowsServiceWithGmsa -Name $serviceName -DisplayName 'Staff Onboarding Provisioning' `
    -Description 'Creates and maintains Active Directory accounts and home folders for Staff Onboarding.' `
    -BinPath (Join-Path $InstallPath 'StaffOnboarding.Provisioning.exe') -Account $account

Start-Service $serviceName
Write-Host "Started $serviceName. Health: http://${HostName}:$Port/health"
