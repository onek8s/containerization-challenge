#Requires -Version 7.2
<#
.SYNOPSIS
  Installs or updates the Windows services of the environment (all, or the ones named with -Project).
.DESCRIPTION
  The services run under virtual accounts (NT SERVICE\<name>) and restart on failure. The .NET runtimes
  needed on the worker host: 2.1 (POS feed), 5.0 (ERP connector, scheduler), 6.0 (notifications), and
  ASP.NET Core 5.0 for the inventory service, which hosts its own HTTPS endpoint (gRPC needs HTTP/2
  with trailers, which IIS on Windows Server 2019 does not offer).
  The ERP connector password is protected with DPAPI for the service account after install
  (VanArsdel.ErpConnector.exe --protect-password); it can only be decrypted on that machine.
.EXAMPLE
  .\Install-Service.ps1 -Environment .\lab.psd1 -Project VanArsdel.PosFeed
#>
param(
    [Parameter(Mandatory)][string] $Environment,
    [string[]] $Project
)
. "$PSScriptRoot\Common.ps1"
$envData = Import-RetailEnvironment $Environment

$services = $envData.Services | Where-Object { -not $Project -or $Project -contains $_.Project }
foreach ($svc in $services) {
    $hostName = Get-HostName $envData $svc.Host
    Write-Host "== $($svc.Name) on $hostName" -ForegroundColor Cyan

    $package = Get-PackagePath $svc.Project
    $template = Join-Path (Join-Path $PSScriptRoot '..\publish') "$($svc.Project)\appsettings.Production.json"
    $settings = New-AppSettingsContent -Environment $envData -Project $svc.Project -Template $template
    $folder = Join-Path $envData.InstallRoot "Services\$($svc.Name)"
    $erpPassword = if ($svc.Project -eq 'VanArsdel.ErpConnector') { Get-RetailSecret $envData 'erp-integration-user' } else { $null }

    $session = New-PSSession -ComputerName $hostName
    try {
        Copy-PackageToHost -Session $session -Package $package -Target $folder -Settings $settings

        Invoke-Command -Session $session -ArgumentList $svc, $folder, $envData.LogRoot, $envData.InstallRoot -ScriptBlock {
            param($svc, $folder, $logRoot, $installRoot)
            $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
            if ($existing -and $existing.Status -ne 'Stopped') {
                Stop-Service -Name $svc.Name -Force
                (Get-Service $svc.Name).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(60))
            }
            if (Test-Path "$folder.old") { Remove-Item "$folder.old" -Recurse -Force }
            if (Test-Path $folder) { Rename-Item $folder "$folder.old" }
            Rename-Item "$folder.new" $folder

            $exe = Join-Path $folder "$($svc.Project).exe"
            if (-not $existing) {
                New-Service -Name $svc.Name -DisplayName $svc.DisplayName -BinaryPathName "`"$exe`"" -StartupType Automatic | Out-Null
                & sc.exe config $svc.Name obj= $svc.Account | Out-Null
                & sc.exe description $svc.Name "VanArsdel retail back office: $($svc.DisplayName)" | Out-Null
            }
            else {
                & sc.exe config $svc.Name binPath= "`"$exe`"" | Out-Null
            }
            # Restart after 1 min, 2 min, then every 5 min; reset the counter after a day.
            & sc.exe failure $svc.Name reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null
            & sc.exe failureflag $svc.Name 1 | Out-Null
            if ($svc.Name -in 'VanArsdelErpConnector', 'VanArsdelScheduler') {
                & sc.exe config $svc.Name start= delayed-auto | Out-Null
            }

            $logDir = Join-Path $logRoot $svc.Name
            New-Item -ItemType Directory -Force -Path $logDir | Out-Null
            & icacls.exe $folder /grant "$($svc.Account):(OI)(CI)RX" | Out-Null
            & icacls.exe $logDir /grant "$($svc.Account):(OI)(CI)M" | Out-Null
            & icacls.exe (Join-Path $folder 'appsettings.Production.json') /inheritance:r /grant:r "BUILTIN\Administrators:(F)" "NT AUTHORITY\SYSTEM:(F)" "$($svc.Account):(R)" | Out-Null

            switch ($svc.Project) {
                'VanArsdel.PosFeed' {
                    foreach ($dir in 'D:\PosFeed\Inbox', 'D:\PosFeed\Archive', 'D:\PosFeed\Error') {
                        New-Item -ItemType Directory -Force -Path $dir | Out-Null
                        & icacls.exe $dir /grant "$($svc.Account):(OI)(CI)M" | Out-Null
                    }
                }
                'VanArsdel.Inventory.Api' {
                    $pfx = Join-Path $installRoot "Certificates\$($svc.HostName).pfx"
                    if (-not (Test-Path $pfx)) { Write-Warning "$pfx missing: export the wildcard certificate for Kestrel." }
                    else { & icacls.exe $pfx /grant "$($svc.Account):(R)" | Out-Null }
                    if (-not (Get-NetFirewallRule -Name 'VanArsdelInventory-5001' -ErrorAction SilentlyContinue)) {
                        New-NetFirewallRule -Name 'VanArsdelInventory-5001' -DisplayName 'VanArsdel Inventory HTTPS/gRPC' -Direction Inbound -Protocol TCP -LocalPort $svc.Port -Action Allow | Out-Null
                    }
                }
                'VanArsdel.ErpConnector' {
                    $secrets = Join-Path $installRoot 'Secrets'
                    New-Item -ItemType Directory -Force -Path $secrets | Out-Null
                    & icacls.exe $secrets /inheritance:r /grant:r "BUILTIN\Administrators:(OI)(CI)F" "NT AUTHORITY\SYSTEM:(OI)(CI)F" "$($svc.Account):(OI)(CI)M" | Out-Null
                }
            }
        }

        if ($erpPassword) {
            # DPAPI LocalMachine scope: the protected file works for the service account on this host only.
            Invoke-Command -Session $session -ArgumentList $folder, $erpPassword, (Join-Path $envData.InstallRoot 'Secrets\erp.bin') -ScriptBlock {
                param($folder, $password, $file)
                & (Join-Path $folder 'VanArsdel.ErpConnector.exe') --protect-password $password --file $file
                if ($LASTEXITCODE -ne 0) { throw 'Protecting the ERP password failed.' }
            }
        }

        Invoke-Command -Session $session -ArgumentList $svc.Name -ScriptBlock {
            param($name)
            Start-Service -Name $name
            (Get-Service $name).WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
        }
    }
    finally {
        Remove-PSSession $session
    }
    if ($svc.ContainsKey('Port')) { Wait-HealthEndpoint "https://$($svc.HostName):$($svc.Port)/health" }
}
