<#
.SYNOPSIS
    Creates the Study Data share on the file server and its DFS folder.
.DESCRIPTION
    Run on files01 (share and NTFS rights), then on a DFS namespace server (folder target), or on one lab
    server for both. Copies the study folders (validated workbooks and report templates) from the repository.

    \\corp.local\dfs\StudyData
        Studies\<study>\calculations   validated calculation workbooks (read only for the applications)
        Studies\<study>\reports        report workbook templates
        SourceDocuments                redacted source documents uploaded by sites
        Outputs                        casebooks, ODM exports, safety reports
        LabTransfers                   central lab files
.EXAMPLE
    .\create-folders.ps1 -Path D:\Shares\StudyData -StudiesSource ..\studies
.EXAMPLE
    .\create-folders.ps1 -Path C:\Shares\StudyData -StudiesSource ..\studies -SkipDfs
#>
param(
    [Parameter(Mandatory)] [string] $Path,
    [string] $ShareName = 'StudyData$',
    [string] $Domain = 'CORP',
    [string] $FileServer = $env:COMPUTERNAME,
    [string] $DfsNamespace = '\\corp.local\dfs',
    [string] $WebAccount = 'CORP\svc-studydata-web',
    [string] $JobsAccount = 'CORP\svc-studydata-job$',
    [string] $AdminsGroup = 'CORP\SD-StudyAdmins',
    [string] $StudiesSource,
    [switch] $SkipDfs
)

$ErrorActionPreference = 'Stop'

foreach ($sub in 'Studies', 'SourceDocuments', 'Outputs', 'LabTransfers') {
    New-Item -ItemType Directory -Force -Path (Join-Path $Path $sub) | Out-Null
}

if ($StudiesSource) {
    Copy-Item (Join-Path $StudiesSource '*') (Join-Path $Path 'Studies') -Recurse -Force
}

# Protected DACL: administrators, study admins (change control of workbooks), the two service accounts.
icacls $Path /inheritance:r /grant:r 'BUILTIN\Administrators:(OI)(CI)F' 'NT AUTHORITY\SYSTEM:(OI)(CI)F' "$($AdminsGroup):(OI)(CI)M" | Out-Null
icacls (Join-Path $Path 'Studies') /grant "$($WebAccount):(OI)(CI)RX" "$($JobsAccount):(OI)(CI)RX" | Out-Null
icacls (Join-Path $Path 'SourceDocuments') /grant "$($WebAccount):(OI)(CI)M" "$($JobsAccount):(OI)(CI)RX" | Out-Null
icacls (Join-Path $Path 'Outputs') /grant "$($WebAccount):(OI)(CI)RX" "$($JobsAccount):(OI)(CI)M" | Out-Null
icacls (Join-Path $Path 'LabTransfers') /grant "$($JobsAccount):(OI)(CI)M" | Out-Null
# Uploaded source documents may not be deleted by the application (only added).
icacls (Join-Path $Path 'SourceDocuments') /deny "$($WebAccount):(OI)(CI)(DE,DC)" | Out-Null

if (-not (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $ShareName -Path $Path -FullAccess 'BUILTIN\Administrators' -ChangeAccess $WebAccount, $JobsAccount, $AdminsGroup | Out-Null
}
Write-Host "Share \\$FileServer\$ShareName -> $Path"

if (-not $SkipDfs) {
    Import-Module DFSN
    $folder = "$DfsNamespace\StudyData"
    if (-not (Get-DfsnFolder -Path $folder -ErrorAction SilentlyContinue)) {
        New-DfsnFolder -Path $folder -TargetPath "\\$FileServer\$ShareName" | Out-Null
    }
    Write-Host "DFS folder $folder -> \\$FileServer\$ShareName"
}
else {
    Write-Host "DFS skipped: set the application settings to \\$FileServer\$ShareName instead of $DfsNamespace\StudyData."
}
