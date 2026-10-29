<#
.SYNOPSIS
    Creates the Trade Rebates file shares (Rebates$, RebatesArchive$) and their folders on the file server.
.DESCRIPTION
    Rebates$
      Inbound\Files      distributor sell-out files (SFTP gateway drops here)
      Inbound\Workbooks  sell-out workbooks e-mailed by distributors
      Inbound\Uploads    workbooks uploaded in the portal
      Processed, Rejected
      Statements         rendered statements
      PriceBook          PriceBook.accdb maintained by sales operations
      Logs
    The SQL Server service account reads and moves the inbound files; the activator account writes statements;
    the portal application pool writes uploads and reads statements.
.EXAMPLE
    .\create-shares.ps1 -Root D:\Shares\Rebates -SqlServiceAccount 'CORP\svc-sql01$' -ActivatorAccount 'CORP\svc-rebates-ea$' `
        -PortalAccount 'CORP\svc-rebates-web$' -ApiAccount 'CORP\svc-rebates-api$' -FilesAccount 'CORP\svc-rebates-files' -SalesOpsGroup 'CORP\TR-SalesOps'
#>
[CmdletBinding()]
param(
    [string] $Root = 'D:\Shares\Rebates',
    [string] $ArchiveRoot = 'D:\Shares\RebatesArchive',
    [Parameter(Mandatory)] [string] $SqlServiceAccount,
    [Parameter(Mandatory)] [string] $ActivatorAccount,
    [Parameter(Mandatory)] [string] $PortalAccount,
    # The price book is opened as the calling Windows login (claim validation from the portal and the API).
    [string] $ApiAccount,
    [Parameter(Mandatory)] [string] $FilesAccount,
    # Windows accounts that run the import jobs (SQL Server Agent service account, or the scheduled task account).
    # The database file routines run as the calling Windows login, so these need the same rights as the SQL Server service.
    [string[]] $JobAccounts = @(),
    [string] $SalesOpsGroup = 'CORP\TR-SalesOps',
    [string] $ShareName = 'Rebates$',
    [string] $ArchiveShareName = 'RebatesArchive$'
)

$ErrorActionPreference = 'Stop'

$folders = 'Inbound\Files', 'Inbound\Workbooks', 'Inbound\Uploads', 'Processed', 'Rejected', 'Statements', 'PriceBook', 'Logs'
foreach ($f in $folders) {
    New-Item -ItemType Directory -Path (Join-Path $Root $f) -Force | Out-Null
}
New-Item -ItemType Directory -Path (Join-Path $ArchiveRoot 'Processed') -Force | Out-Null

function Grant-Folder([string] $Path, [string] $Account, [string] $Rights) {
    # (OI)(CI) inherit to files and sub folders
    & icacls.exe $Path /grant "${Account}:(OI)(CI)$Rights" /T /C /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls failed for $Path ($Account)" }
}

# Start from Administrators/SYSTEM only.
& icacls.exe $Root /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' /Q | Out-Null

foreach ($account in @($SqlServiceAccount) + $JobAccounts) {
    Grant-Folder $Root $account 'RX'
    foreach ($f in 'Inbound', 'Processed', 'Rejected') { Grant-Folder (Join-Path $Root $f) $account 'M' }
}
Grant-Folder (Join-Path $Root 'PriceBook') $SqlServiceAccount 'M'      # ACE writes the .laccdb lock file next to the database
Grant-Folder (Join-Path $Root 'Statements') $ActivatorAccount 'M'
Grant-Folder (Join-Path $Root 'Inbound\Uploads') $PortalAccount 'M'
Grant-Folder (Join-Path $Root 'Statements') $PortalAccount 'RX'
Grant-Folder $Root $FilesAccount 'M'
Grant-Folder (Join-Path $Root 'PriceBook') $SalesOpsGroup 'M'
foreach ($account in @($PortalAccount, $ApiAccount) | Where-Object { $_ }) { Grant-Folder (Join-Path $Root 'PriceBook') $account 'M' }
Grant-Folder (Join-Path $Root 'Inbound\Workbooks') $SalesOpsGroup 'M'

& icacls.exe $ArchiveRoot /inheritance:r /grant 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' "${FilesAccount}:(OI)(CI)M" /Q | Out-Null

foreach ($share in @(@{ Name = $ShareName; Path = $Root }, @{ Name = $ArchiveShareName; Path = $ArchiveRoot })) {
    if (-not (Get-SmbShare -Name $share.Name -ErrorAction SilentlyContinue)) {
        New-SmbShare -Name $share.Name -Path $share.Path -FullAccess 'BUILTIN\Administrators' -ChangeAccess 'Authenticated Users' `
            -FolderEnumerationMode AccessBased -CachingMode None | Out-Null
        Write-Host "Created share \\$env:COMPUTERNAME\$($share.Name) -> $($share.Path)"
    }
}
