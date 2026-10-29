<#
.SYNOPSIS
    Creates the record vault folder on the signing host.
.DESCRIPTION
    The vault must be on a local NTFS volume. Only SYSTEM, Administrators and the signing host account
    have access, inheritance is cut, and the folder is marked for EFS encryption so every file the signing
    host writes is encrypted with the signing host account's EFS certificate.
    Before going live make sure an EFS data recovery agent is in place (domain: Default Domain Policy >
    Public Key Policies > Encrypting File System; check with 'cipher /r' and 'cipher /c <file>').
.EXAMPLE
    .\create-vault.ps1 -Path D:\ReleaseVault -ServiceAccount 'CORP\svc-release-sign'
#>
[CmdletBinding()]
param(
    [string] $Path = 'D:\ReleaseVault',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [string[]] $BackupReaders = @()
)

$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetPathRoot((Resolve-Path -LiteralPath (Split-Path $Path -Parent)).Path)
$volume = Get-Volume -FilePath $root
if ($volume.FileSystem -ne 'NTFS') { throw "$root is $($volume.FileSystem); the vault needs NTFS." }

New-Item -ItemType Directory -Path $Path -Force | Out-Null

$acl = New-Object System.Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
$inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
$none = [System.Security.AccessControl.PropagationFlags]::None
foreach ($principal in @('NT AUTHORITY\SYSTEM', 'BUILTIN\Administrators', $ServiceAccount)) {
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($principal, 'FullControl', $inherit, $none, 'Allow')))
}
foreach ($reader in $BackupReaders) {
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($reader, 'ReadAndExecute', $inherit, $none, 'Allow')))
}
$acl.SetOwner((New-Object System.Security.Principal.NTAccount('BUILTIN\Administrators')))
Set-Acl -LiteralPath $Path -AclObject $acl

# Mark the folder so new files are encrypted by whoever creates them (the signing host account).
& cipher.exe /e /s:"$Path" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'cipher /e failed.' }

if (-not ((Get-Item -LiteralPath $Path).Attributes -band [IO.FileAttributes]::Encrypted)) {
    throw "$Path is not marked for encryption. Is EFS disabled by policy?"
}
Write-Host "Vault $Path ready (NTFS, protected ACL, EFS)."
