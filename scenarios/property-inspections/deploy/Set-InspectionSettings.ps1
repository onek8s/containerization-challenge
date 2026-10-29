<#
.SYNOPSIS
    Writes the machine settings to HKLM\SOFTWARE\Northwind Insurance\Inspections on the service host and the
    application server. Passwords are not stored by this script: run, as each service account,
        runas /user:CORP\svc-insp-svc "C:\Program Files\Northwind\Inspections\Inspections.Jobs.exe protect-secret SharePassword"
.EXAMPLE
    .\Set-InspectionSettings.ps1 -ZoneName corp.local -ShareUser CORP\svc-insp-share
#>
param(
    [Parameter(Mandatory)] [string]$ZoneName,
    [string]$SqlServer = "sql01.$ZoneName",
    [string]$PhotoShare = "\\files01.$ZoneName\Inspections$",
    [string]$ShareUser,
    [string]$SmtpHost = "smtp.$ZoneName",
    [int]$SmtpPort = 25,
    [string]$SmtpFrom = 'inspections@northwind-insurance.example',
    [string]$SmtpUser,
    [string]$PortalUrl = "https://inspections.$ZoneName",
    [int]$PurgeAfterDays = 400,
    [string[]]$SecretReaders = @()
)

$ErrorActionPreference = 'Stop'
$key = 'HKLM:\SOFTWARE\Northwind Insurance\Inspections'

# New-Item -Force would recreate an existing key and delete the stored secrets.
foreach ($path in $key, "$key\Secrets") {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
}

$values = [ordered]@{
    ConnectionString = "Data Source=$SqlServer;Initial Catalog=Inspections;Integrated Security=SSPI;Application Name=Inspections"
    PhotoShare       = $PhotoShare
    ShareUser        = $ShareUser
    SmtpHost         = $SmtpHost
    SmtpFrom         = $SmtpFrom
    SmtpUser         = $SmtpUser
    PortalUrl        = $PortalUrl
    WatermarkFont    = 'Segoe UI'
    WorkFolder       = '%ProgramData%\Northwind\Inspections\Work'
}
foreach ($name in $values.Keys) {
    if ($values[$name]) {
        New-ItemProperty -Path $key -Name $name -Value $values[$name] -PropertyType ExpandString -Force | Out-Null
    }
}

$numbers = [ordered]@{ SmtpPort = $SmtpPort; MaxPhotoEdge = 2048; ThumbnailEdge = 320; JpegQuality = 85; PurgeAfterDays = $PurgeAfterDays; ReportBatchSize = 25 }
foreach ($name in $numbers.Keys) {
    New-ItemProperty -Path $key -Name $name -Value $numbers[$name] -PropertyType DWord -Force | Out-Null
}

# Service accounts write their own protected secrets under Secrets\<DOMAIN_user>.
if ($SecretReaders) {
    $acl = Get-Acl "$key\Secrets"
    foreach ($account in $SecretReaders) {
        $rule = New-Object System.Security.AccessControl.RegistryAccessRule($account, 'ReadKey,CreateSubKey,SetValue', 'ContainerInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    Set-Acl "$key\Secrets" $acl
}

Write-Host "Settings written to $key"
