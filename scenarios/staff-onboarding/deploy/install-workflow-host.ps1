<#
.SYNOPSIS
    Installs the Staff Onboarding workflow host Windows service on onboarding-flow.corp.local.
.DESCRIPTION
    Run as administrator on the workflow server (Windows Server 2019 or later, .NET Framework 4.8,
    domain joined, allowed to retrieve the gMSA password). Copy the build output first, for example
    dotnet publish src\StaffOnboarding.WorkflowHost -c Release -o C:\Install\WorkflowHost
#>
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [string] $InstallPath = 'C:\Services\StaffOnboarding\WorkflowHost',
    [string] $Domain = 'CORP',
    [string] $ServiceAccount = 'svc-onbflow',
    [int] $Port = 8085,
    [string] $PortalServerAddress = 'onboarding.corp.local',
    [string] $LogPath = 'D:\Logs\StaffOnboarding'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$account = "$Domain\$ServiceAccount$"
$serviceName = 'StaffOnboardingWorkflow'

Install-Gmsa $ServiceAccount
Grant-ServiceLogonRight $account
Ensure-EventSource 'Staff Onboarding Workflow'

if (Get-Service $serviceName -ErrorAction SilentlyContinue) { Stop-Service $serviceName -Force }
New-Item -ItemType Directory -Force -Path $InstallPath, $LogPath | Out-Null
robocopy $SourcePath $InstallPath /MIR /XF '*.config' /NFL /NDL /NJH /NJS | Out-Null
$config = Join-Path $InstallPath 'StaffOnboarding.WorkflowHost.exe.config'
if (-not (Test-Path $config)) { Copy-Item (Join-Path $SourcePath 'StaffOnboarding.WorkflowHost.exe.config') $config }
icacls $LogPath /grant "${account}:(OI)(CI)M" | Out-Null

# http.sys reservation for the workflow endpoints and the firewall rule for the portal server
netsh http delete urlacl url="http://+:$Port/StaffOnboarding/" 2>$null | Out-Null
netsh http add urlacl url="http://+:$Port/StaffOnboarding/" user="$account" | Out-Host
$portal = (Resolve-DnsName $PortalServerAddress -Type A).IPAddress
if (-not (Get-NetFirewallRule -Name 'StaffOnboardingWorkflow' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'StaffOnboardingWorkflow' -DisplayName 'Staff Onboarding workflow host' -Direction Inbound `
        -Protocol TCP -LocalPort $Port -RemoteAddress $portal -Action Allow | Out-Null
}

Install-WindowsServiceWithGmsa -Name $serviceName -DisplayName 'Staff Onboarding Workflow Host' `
    -Description 'Runs joiner, mover and leaver workflows for Staff Onboarding.' `
    -BinPath (Join-Path $InstallPath 'StaffOnboarding.WorkflowHost.exe') -Account $account

Start-Service $serviceName
Write-Host "Started $serviceName. Check: StaffOnboarding.WorkflowHost.exe instances"
