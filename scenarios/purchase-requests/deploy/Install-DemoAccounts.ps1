<#
.SYNOPSIS
    Creates local demo users and groups for a single-server test installation without Active Directory.
    Use the computer name as AccountDomain for db\03-seed.sql and in the group settings of the API Web.config.
#>
param(
    [string]$Password = 'Purchasing#2026'
)

$ErrorActionPreference = 'Stop'
$secure = ConvertTo-SecureString $Password -AsPlainText -Force

foreach ($g in 'PUR-Finance', 'PUR-Directors', 'PUR-Buyers', 'PUR-Admins') {
    if (-not (Get-LocalGroup -Name $g -ErrorAction SilentlyContinue)) { New-LocalGroup -Name $g | Out-Null }
}

$users = @(
    @{ Name = 'jkoski'; FullName = 'Jari Koski';  Groups = @() },
    @{ Name = 'mlind';  FullName = 'Maria Lind';  Groups = @() },
    @{ Name = 'ksalo';  FullName = 'Kalle Salo';  Groups = @() },
    @{ Name = 'pvirta'; FullName = 'Pekka Virta'; Groups = @('PUR-Finance') },
    @{ Name = 'ahalme'; FullName = 'Anna Halme';  Groups = @('PUR-Directors') },
    @{ Name = 'tniemi'; FullName = 'Tiina Niemi'; Groups = @('PUR-Buyers') }
)

foreach ($u in $users) {
    if (-not (Get-LocalUser -Name $u.Name -ErrorAction SilentlyContinue)) {
        New-LocalUser -Name $u.Name -FullName $u.FullName -Password $secure -PasswordNeverExpires | Out-Null
    }
    Add-LocalGroupMember -Group 'Users' -Member $u.Name -ErrorAction SilentlyContinue
    foreach ($g in $u.Groups) { Add-LocalGroupMember -Group $g -Member $u.Name -ErrorAction SilentlyContinue }
    Write-Host "$env:COMPUTERNAME\$($u.Name) ($($u.FullName)) $($u.Groups -join ', ')"
}
