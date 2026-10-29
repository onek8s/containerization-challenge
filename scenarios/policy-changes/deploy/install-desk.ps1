<#
.SYNOPSIS
    Prepares a desk station: a Windows machine that signs in automatically as the desk user
    and runs the desk runner next to PolicyDesk on that user's desktop.
.DESCRIPTION
    Run as administrator on the desk station (desk01, desk02, ...). Prerequisites:
      - PolicyDesk 6.4 client installed (Halcyon installer, one seat licence per desk user)
      - .NET 8 Desktop Runtime and .NET 8 runtime
      - Desk user in AD (e.g. CORP\svc-desk01), member of 'CORP\Policy Desk Stations',
        with a PolicyDesk user of its own
    Publish first:
      dotnet publish src\PolicyChanges.DeskRunner -c Release -r win-x64 --self-contained false -o publish\desk

    Afterwards sign in once as the desk user and run
      C:\PolicyChanges\DeskRunner\PolicyChanges.DeskRunner.exe store-credential
      C:\PolicyChanges\DeskRunner\PolicyChanges.DeskRunner.exe check
.EXAMPLE
    .\install-desk.ps1 -PublishPath ..\publish\desk -DeskUser 'CORP\svc-desk01' -AutologonTool C:\Tools\Autologon64.exe
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [Parameter(Mandatory)] [string] $DeskUser,
    [string] $InstallPath = 'C:\PolicyChanges\DeskRunner',
    [string] $StationName = $env:COMPUTERNAME,
    [string] $EvidenceShare = '\\files01.corp.local\PolicyChanges$\Evidence',
    # Sysinternals Autologon stores the password as an LSA secret. Without it the password
    # is written to the Winlogon registry key in clear text.
    [string] $AutologonTool,
    [int] $ScreenWidth = 1280,
    [int] $ScreenHeight = 1024
)

$ErrorActionPreference = 'Stop'

$domain, $user = $DeskUser.Split('\', 2)
$credential = Get-Credential -UserName $DeskUser -Message 'Desk user (Windows sign-in)'

# 1. Desk runner
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force
icacls $InstallPath /grant "$($DeskUser):(OI)(CI)RX" | Out-Null

$settings = Join-Path $InstallPath 'appsettings.Production.json'
@{
    Desk = @{ StationName = $StationName; EvidenceRoot = $EvidenceShare }
} | ConvertTo-Json -Depth 3 | Set-Content -Path $settings -Encoding UTF8

# 2. Automatic sign-in of the desk user after every restart
if ($AutologonTool) {
    & $AutologonTool $user $domain $credential.GetNetworkCredential().Password /accepteula
} else {
    Write-Warning 'Autologon tool not given; storing the desk user password in the Winlogon registry key.'
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Set-ItemProperty $winlogon -Name AutoAdminLogon -Value '1'
    Set-ItemProperty $winlogon -Name DefaultUserName -Value $user
    Set-ItemProperty $winlogon -Name DefaultDomainName -Value $domain
    Set-ItemProperty $winlogon -Name DefaultPassword -Value $credential.GetNetworkCredential().Password
}
# No legal notice / Ctrl+Alt+Del prompts that would stop the automatic sign-in
$system = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System'
Set-ItemProperty $system -Name DisableCAD -Value 1 -Type DWord
Set-ItemProperty $system -Name legalnoticecaption -Value ''
Set-ItemProperty $system -Name legalnoticetext -Value ''

# 3. The desktop must stay visible and unlocked: no lock screen, screen saver, display or sleep timeout
Set-ItemProperty $system -Name InactivityTimeoutSecs -Value 0 -Type DWord
New-Item -Force 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization' | Out-Null
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization' -Name NoLockScreen -Value 1 -Type DWord
powercfg /change monitor-timeout-ac 0
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0

$sid = (New-Object System.Security.Principal.NTAccount($DeskUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$hive = "HKU\$sid"
$profileLoaded = Test-Path "Registry::HKEY_USERS\$sid"
if (-not $profileLoaded) {
    $profilePath = (Get-CimInstance Win32_UserProfile | Where-Object SID -eq $sid).LocalPath
    if ($profilePath) {
        reg load $hive (Join-Path $profilePath 'NTUSER.DAT') | Out-Null
        $profileLoaded = $true
        $unload = $true
    }
}
if ($profileLoaded) {
    $desktopKey = "Registry::HKEY_USERS\$sid\Software\Policies\Microsoft\Windows\Control Panel\Desktop"
    New-Item -Force $desktopKey | Out-Null
    Set-ItemProperty $desktopKey -Name ScreenSaveActive -Value '0'
    Set-ItemProperty $desktopKey -Name ScreenSaverIsSecure -Value '0'
    if ($unload) { [GC]::Collect(); reg unload $hive | Out-Null }
} else {
    Write-Warning "Profile of $DeskUser does not exist yet; sign in once and run this script again to turn off the screen saver."
}

# 4. Console resolution: PolicyDesk windows must fit on the screen for screenshots
Write-Host "Set the console display of this machine (or the VM console) to at least ${ScreenWidth}x${ScreenHeight}."

# 5. Start the desk runner when the desk user signs in, on that user's desktop
$action = New-ScheduledTaskAction -Execute (Join-Path $InstallPath 'PolicyChanges.DeskRunner.exe') -WorkingDirectory $InstallPath
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $DeskUser
$principal = New-ScheduledTaskPrincipal -UserId $DeskUser -LogonType Interactive -RunLevel Limited
$taskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'Policy Changes desk runner' -Action $action -Trigger $trigger -Principal $principal `
    -Settings $taskSettings -Force | Out-Null

# 6. Remote desktop: disconnecting an RDP session leaves no desktop for the runner.
Copy-Item (Join-Path $PSScriptRoot 'return-to-console.cmd') (Join-Path $InstallPath 'return-to-console.cmd') -Force
Write-Host @"
Desk station $StationName prepared for $DeskUser.
Next steps:
  1. Restart; the machine signs in as $DeskUser.
  2. As ${DeskUser}: $InstallPath\PolicyChanges.DeskRunner.exe store-credential
  3. As ${DeskUser}: $InstallPath\PolicyChanges.DeskRunner.exe check
  4. When you connect with Remote Desktop, leave with $InstallPath\return-to-console.cmd instead of disconnecting.
"@
