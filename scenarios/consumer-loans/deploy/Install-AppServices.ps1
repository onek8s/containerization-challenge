<#
.SYNOPSIS
  Installs the Windows services LucerneLendingDocuments and LucerneLendingCollections and the monthly
  loan tape scheduled task on lending-app01, from the published output in .\out.
#>
param(
    [string] $Config = (Join-Path $PSScriptRoot 'environment.psd1'),
    [string] $Secrets,
    [string] $Source = (Join-Path $PSScriptRoot '..\out')
)
Import-Module (Join-Path $PSScriptRoot 'Common.psm1') -Force
$cfg = Import-DeployConfig $Config $Secrets

$account = $cfg.Services.Account
$password = Get-Secret $cfg 'Service.Password'
$root = $cfg.InstallRoot
$logs = $cfg.LogRoot
$interfaces = $cfg.InterfaceRoot
$core = Get-CoreSettings $cfg 2
$api = "https://$(Get-HostName $cfg 'lending-api')"
$mail = if ($cfg.Urls.Smtp) { @{ Host = $cfg.Urls.Smtp; Port = 25 } } else { @{ Host = ''; PickupFolder = (Join-Path $root 'Mail') } }

$apps = [ordered]@{
    'documents' = @{
        Service  = $cfg.Services.Documents
        Display  = 'Lucerne Lending Documents'
        Exe      = 'Lucerne.Lending.Documents.exe'
        Settings = @{
            ConnectionStrings = @{ Lending = Get-SqlConnectionString $cfg $cfg.Sql.LendingDatabase 'lending_docs' 'Lending Documents' }
            ESign             = @{ BaseUrl = $cfg.Urls.ESign; ApiKey = (Get-Secret $cfg 'ESign.ApiKey' 'lab-esign-key'); CallbackUrl = "$api/api/esign/callback";
                                   ReturnUrl = "https://$(Get-HostName $cfg 'apply')/applications" }
            Mail              = $mail
            Portal            = @{ BaseUrl = "https://$(Get-HostName $cfg 'apply')" }
            NLog              = Get-LogSettings (Join-Path $logs 'Documents')
        }
    }
    'collections' = @{
        Service  = $cfg.Services.Collections
        Display  = 'Lucerne Lending Collections'
        Exe      = 'Lucerne.Lending.Collections.exe'
        Settings = @{
            ConnectionStrings = @{ Lending = Get-SqlConnectionString $cfg $cfg.Sql.LendingDatabase 'lending_coll' 'Lending Collections' }
            Core              = $core
            GeneralLedger     = @{ OutboundFolder = (Join-Path $interfaces 'GL\Outbound') }
            Collections       = @{ AgencyOutboundFolder = (Join-Path $interfaces 'Agency\Outbound') }
            PaymentsHub       = @{ BaseUrl = $cfg.Urls.PaymentsHub; ClientSecret = (Get-Secret $cfg 'PaymentsHub.ClientSecret' 'lab') }
            NLog              = Get-LogSettings (Join-Path $logs 'Collections')
        }
    }
}

Grant-UserRight $account 'SeServiceLogonRight'
$identity = if ($account -eq 'LocalSystem') { 'NT AUTHORITY\SYSTEM' } else { $account }

foreach ($name in $apps.Keys) {
    $app = $apps[$name]
    Write-Step $app.Service
    $path = New-Folder (Join-Path $root $name)
    $existing = Get-Service -Name $app.Service -ErrorAction SilentlyContinue
    if ($existing -and $existing.Status -ne 'Stopped') { Stop-Service -Name $app.Service -Force; $existing.WaitForStatus('Stopped', '00:01:00') }

    robocopy (Join-Path $Source $name) $path /MIR /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Copy to $path failed ($LASTEXITCODE)." }
    Write-ProductionSettings $path $app.Settings

    Grant-FolderAccess (Join-Path $logs ($name.Substring(0, 1).ToUpper() + $name.Substring(1))) $identity
    if ($name -eq 'collections') {
        Grant-FolderAccess (Join-Path $interfaces 'GL\Outbound') $identity
        Grant-FolderAccess (Join-Path $interfaces 'Agency\Outbound') $identity
        if ($cfg.Core.Provider -eq 'Simulator') { Grant-FolderAccess (Split-Path $cfg.Core.SimulatorDatabase) $identity }
    }
    if ($mail.ContainsKey('PickupFolder') -and $name -eq 'documents') { Grant-FolderAccess $mail.PickupFolder $identity }

    $binary = "`"$(Join-Path $path $app.Exe)`" --environment Production"
    if (-not $existing) {
        if ($account -eq 'LocalSystem') {
            New-Service -Name $app.Service -DisplayName $app.Display -BinaryPathName $binary -StartupType Automatic | Out-Null
        } else {
            $secure = if ($password) { ConvertTo-SecureString $password -AsPlainText -Force } else { New-Object System.Security.SecureString }
            $credential = New-Object System.Management.Automation.PSCredential($account, $secure)
            New-Service -Name $app.Service -DisplayName $app.Display -BinaryPathName $binary -StartupType Automatic -Credential $credential | Out-Null
        }
    }
    sc.exe config $app.Service start= delayed-auto | Out-Null
    sc.exe failure $app.Service reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
    sc.exe description $app.Service "Consumer Loans $name processing" | Out-Null
    Start-Service -Name $app.Service
    Write-Host "    $($app.Service) running as $account"
}

Write-Step $cfg.Services.LoanTapeTask
$tape = New-Folder (Join-Path $root 'loantape')
robocopy (Join-Path $Source 'loantape') $tape /MIR /XF appsettings.Production.json /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copy to $tape failed ($LASTEXITCODE)." }
$riskDw = $cfg.RiskDw
$tapeSettings = @{
    ConnectionStrings = @{ Lending = Get-SqlConnectionString $cfg $cfg.Sql.LendingDatabase 'lending_tape' 'Loan Tape' }
    LoanTape          = @{ Folder = (Join-Path $interfaces 'RiskDW\Outbound') }
}
if ($riskDw.ContainsKey('Target') -and $riskDw.Target -eq 'Folder') {
    $tapeSettings.LoanTape.Target = 'Folder'
} else {
    $tapeSettings.LoanTape.Target = 'Db2'
    $tapeSettings.LoanTape.Schema = $riskDw.Schema
    $tapeSettings.ConnectionStrings.RiskDw = "Server=$($riskDw.Server);Database=$($riskDw.Database);UID=$($riskDw.User);PWD=$(Get-Secret $cfg 'RiskDw.Password');Security=SSL;CurrentSchema=$($riskDw.Schema);"
}
Write-ProductionSettings $tape $tapeSettings
Grant-FolderAccess (Join-Path $interfaces 'RiskDW\Outbound') $identity
Grant-FolderAccess (Join-Path $logs 'LoanTape') $identity

# first working day of the month 06:30: the task runs daily and the program exits unless it is the first working day
Grant-UserRight $account 'SeBatchLogonRight'
$action = New-ScheduledTaskAction -Execute (Join-Path $tape 'Lucerne.Lending.LoanTape.exe') -Argument '--environment Production --first-working-day-only true' -WorkingDirectory $tape
$trigger = New-ScheduledTaskTrigger -Daily -At '06:30'
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -MultipleInstances IgnoreNew
if ($account -eq 'LocalSystem') {
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Limited
    Register-ScheduledTask -TaskName $cfg.Services.LoanTapeTask -TaskPath '\Lucerne\' -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
} else {
    Register-ScheduledTask -TaskName $cfg.Services.LoanTapeTask -TaskPath '\Lucerne\' -Action $action -Trigger $trigger -Settings $settings -User $account -Password $password -RunLevel Limited -Force | Out-Null
}
Write-Host "    \Lucerne\$($cfg.Services.LoanTapeTask) daily 06:30"
