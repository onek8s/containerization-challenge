<#
.SYNOPSIS
    Sets up sftp.<zone>: OpenSSH server with chrooted SFTP-only accounts for the warehouse and the marketplace.
.DESCRIPTION
    The 3PL warehouse (A. Datum Logistics) and the marketplace (Trailmarket) log in to Litware's SFTP server.
    Each account gets a chroot folder under -Root with the folders the hub reads and writes.
    The order hub logs in with the same accounts (password or the RSA key given in -AuthorizedKeys).
.EXAMPLE
    .\Install-SftpServer.ps1 -Root D:\Sftp -WarehousePassword (Read-Host -AsSecureString) -MarketplacePassword (Read-Host -AsSecureString) `
        -AuthorizedKeys @{ 'adatum-wms' = 'C:\Install\orderhub-wms.pub' }
#>
param(
    [string] $Root = 'C:\SftpRoot',
    [Parameter(Mandatory)] [securestring] $WarehousePassword,
    [Parameter(Mandatory)] [securestring] $MarketplacePassword,
    [hashtable] $AuthorizedKeys = @{},
    [int] $Port = 22
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Administrator

$capability = Get-WindowsCapability -Online -Name 'OpenSSH.Server*'
if ($capability.State -ne 'Installed') {
    Add-WindowsCapability -Online -Name $capability.Name | Out-Null
}
Set-Service sshd -StartupType Automatic
Start-Service sshd

$accounts = [ordered]@{
    'adatum-wms'  = @{ Password = $WarehousePassword; Folders = @('in', 'out', 'out\archive', 'out\error', 'out\stock', 'out\stock\archive') }
    'trailmarket' = @{ Password = $MarketplacePassword; Folders = @('orders', 'orders\processed', 'orders\rejected', 'confirmations', 'stock') }
}

$config = Join-Path $env:ProgramData 'ssh\sshd_config'
$text = Get-Content $config -Raw
$begin = '# BEGIN order hub'
$end = '# END order hub'
if ($text -match [regex]::Escape($begin)) {
    $text = [regex]::Replace($text, "(?s)$([regex]::Escape($begin)).*?$([regex]::Escape($end))\r?\n?", '')
}

# SSH.NET 2020 signs with ssh-rsa (SHA-1); current OpenSSH only accepts it when allowed explicitly.
# These are global settings and must come before the first Match block of the default config.
$globalBegin = '# BEGIN order hub (global)'
$globalEnd = '# END order hub (global)'
$text = [regex]::Replace($text, "(?s)$([regex]::Escape($globalBegin)).*?$([regex]::Escape($globalEnd))\r?\n?", '')
$globalSettings = ($globalBegin, 'PubkeyAcceptedAlgorithms +ssh-rsa', 'HostKeyAlgorithms +ssh-rsa', $globalEnd) -join "`r`n"
$firstMatch = [regex]::Match($text, '(?m)^Match\s')
$text = if ($firstMatch.Success) { $text.Insert($firstMatch.Index, $globalSettings + "`r`n") } else { $text.TrimEnd() + "`r`n" + $globalSettings + "`r`n" }

$block = @($begin)

foreach ($name in $accounts.Keys) {
    New-LocalServiceAccount -Name $name -Password $accounts[$name].Password -Description 'SFTP account'
    # Recent Windows Server builds ship sshd_config with 'AllowGroups administrators "openssh users"'.
    $members = net.exe localgroup "OpenSSH Users" 2>$null
    if ($LASTEXITCODE -eq 0 -and -not ($members -contains $name)) { net.exe localgroup "OpenSSH Users" $name /add | Out-Null }
    $accountRoot = Join-Path $Root $name
    New-Item -ItemType Directory -Force -Path $accountRoot | Out-Null
    foreach ($folder in $accounts[$name].Folders) {
        Grant-FolderAccess -Path (Join-Path $accountRoot $folder) -Account "$env:COMPUTERNAME\$name"
    }

    if ($AuthorizedKeys.ContainsKey($name)) {
        $sshDir = Join-Path $accountRoot '.ssh'
        New-Item -ItemType Directory -Force -Path $sshDir | Out-Null
        Copy-Item $AuthorizedKeys[$name] (Join-Path $sshDir 'authorized_keys') -Force
        icacls.exe (Join-Path $sshDir 'authorized_keys') /inheritance:r /grant "SYSTEM:F" /grant "Administrators:F" /grant "$env:COMPUTERNAME\${name}:R" | Out-Null
    }

    $block += "Match User $name"
    $block += "    ChrootDirectory $($accountRoot -replace '\\', '/')"
    $block += '    ForceCommand internal-sftp'
    $block += '    AllowTcpForwarding no'
    $block += '    PermitTunnel no'
    $block += "    AuthorizedKeysFile $(($accountRoot -replace '\\', '/'))/.ssh/authorized_keys"
}
$block += $end

# Match blocks must come after the global settings, and the default config ends with one for administrators.
$text = $text.TrimEnd() + "`r`n`r`n" + ($block -join "`r`n") + "`r`n"
if ($Port -ne 22) { $text = $text -replace '(?m)^#?Port\s+\d+', "Port $Port" }
Set-Content -Path $config -Value $text -Encoding ascii

Restart-Service sshd
if (-not (Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}

# SSH.NET negotiates the ed25519 host key when the server has one, otherwise ECDSA or RSA.
$hostKey = 'ed25519', 'ecdsa', 'rsa' | ForEach-Object { Join-Path $env:ProgramData "ssh\ssh_host_$($_)_key.pub" } | Where-Object { Test-Path $_ } | Select-Object -First 1
$fingerprint = & ssh-keygen.exe -l -E md5 -f $hostKey
Write-Host "SFTP ready. Host key: $fingerprint"
Write-Host 'Use the md5 fingerprint without the MD5: prefix in *.Sftp.HostKeyFingerprint.'
