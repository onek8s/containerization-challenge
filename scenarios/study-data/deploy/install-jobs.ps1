<#
.SYNOPSIS
    Installs the Study Data jobs service (StudyDataJobs) on trials-app01.
.DESCRIPTION
    Prerequisites: .NET 8 runtime and Windows Desktop runtime 8 (x64), install-office-automation.ps1,
    install-pdf-printer.ps1 and install-coding-engine.ps1 on this server, the central lab SFTP key in
    C:\StudyData\Keys. Publish first:
      dotnet publish src\StudyData.Jobs -c Release -r win-x64 --self-contained false -o publish\jobs
.EXAMPLE
    .\install-jobs.ps1 -PublishPath ..\publish\jobs -ServiceAccount 'CORP\svc-studydata-job$'
.EXAMPLE
    .\install-jobs.ps1 -PublishPath ..\publish\jobs -ServiceAccount '.\svc-studydata-job' -Password (Read-Host -AsSecureString)
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [securestring] $Password,
    [string] $InstallPath = 'C:\Program Files\Fabrikam\StudyData Jobs',
    [string] $DomainName = 'corp.local',
    [string] $SqlServer = 'sql01',
    [string] $DataRoot = '\\corp.local\dfs\StudyData',
    [string] $SmtpHost = 'smtp.corp.local',
    [string] $LabHost = 'sftp.centrallab.example',
    [string] $LabHostKeySha256 = ''
)

$ErrorActionPreference = 'Stop'
$serviceName = 'StudyDataJobs'

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force

foreach ($dir in 'C:\StudyData\Logs', 'C:\StudyData\Keys', 'C:\StudyData\Spool', 'C:\StudyData\Workbooks') {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}
icacls 'C:\StudyData\Logs' /grant "$($ServiceAccount):(OI)(CI)M" | Out-Null
icacls 'C:\StudyData\Keys' /inheritance:r /grant:r 'BUILTIN\Administrators:(OI)(CI)F' "$($ServiceAccount):(OI)(CI)R" | Out-Null

# Settings next to the executable (appsettings.Production.json overrides appsettings.json).
$settings = [ordered]@{
    ConnectionStrings = @{ StudyData = "Server=$SqlServer.$DomainName;Database=StudyData;Integrated Security=true;TrustServerCertificate=true;Application Name=StudyData.Jobs" }
    Calculation       = @{ WorkbookRoot = "$DataRoot\Studies" }
    CentralLab        = @{ Host = $LabHost; HostKeySha256 = $LabHostKeySha256; LocalFolder = "$DataRoot\LabTransfers" }
    Mail              = @{ Host = $SmtpHost }
    Exports           = @{ OutputRoot = "$DataRoot\Outputs" }
}
$settings | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $InstallPath 'appsettings.Production.json') -Encoding UTF8

# "Log on as a service" for the account: export the current policy, merge, import (never replace).
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount.TrimStart('.\'))).Translate([System.Security.Principal.SecurityIdentifier]).Value
$work = Join-Path $env:TEMP "studydata-secedit-$(Get-Random)"
New-Item -ItemType Directory -Path $work | Out-Null
try {
    secedit /export /cfg "$work\current.inf" /areas USER_RIGHTS | Out-Null
    $line = (Get-Content "$work\current.inf" -Encoding Unicode) | Where-Object { $_ -like 'SeServiceLogonRight*' }
    if ($line -notmatch [regex]::Escape("*$sid")) {
        $value = if ($line) { "$line,*$sid" } else { "SeServiceLogonRight = *$sid" }
        @('[Unicode]', 'Unicode=yes', '[Version]', 'signature="$CHICAGO$"', 'Revision=1', '[Privilege Rights]', $value) |
            Set-Content "$work\merge.inf" -Encoding Unicode
        secedit /configure /db "$work\merge.sdb" /cfg "$work\merge.inf" /areas USER_RIGHTS | Out-Null
    }
}
finally {
    Remove-Item $work -Recurse -Force
}

$exe = Join-Path $InstallPath 'StudyData.Jobs.exe'
if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto DisplayName= 'Study Data jobs' | Out-Null
}
$plain = if ($Password) { [System.Net.NetworkCredential]::new('', $Password).Password } else { '' }
sc.exe config $serviceName obj= $ServiceAccount password= $plain | Out-Null
sc.exe description $serviceName 'Casebook PDFs, ODM exports, DSMB safety workbooks, medical coding, central lab import and query reminders for Study Data.' | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
[Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Production', 'Machine')

if (-not [System.Diagnostics.EventLog]::SourceExists('StudyData Jobs')) {
    New-EventLog -LogName Application -Source 'StudyData Jobs'
}

Start-Service $serviceName
Get-Service $serviceName
Write-Host "Run a job now: & '$exe' run documents|casebooks|safety|coding|labs|reminders"
