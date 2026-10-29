<#
.SYNOPSIS
    Installs the Furnace Records worker service (MES events, certificate archive, calibration reminders).
.DESCRIPTION
    Publish first:
      dotnet publish src\FurnaceRecords.Worker -c Release -r win-x64 --self-contained false -o publish\worker
.EXAMPLE
    .\install-worker.ps1 -PublishPath ..\publish\worker -ServiceAccount 'CORP\svc-furnace-wrk$'
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $InstallPath = 'C:\Services\FurnaceRecords.Worker',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [string] $DomainName = 'corp.local',
    [string] $SqlServer = 'sql01',
    [string] $FilesHost = 'files01',
    [string] $MesAddress = 'https://mes.corp.local/api/heat-treat/',
    [string] $SmtpHost = 'smtp',
    [string[]] $ReminderRecipients = @('quality-heat-treat@corp.local'),
    [string] $ServiceName = 'FurnaceRecords.Worker'
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force
}
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force
$exe = Join-Path $InstallPath 'FurnaceRecords.Worker.exe'

if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Furnace Records Worker' `
        -Description 'Sends load events to MES, archives certificates and sends calibration reminders.' -StartupType Automatic | Out-Null
    if ($ServiceAccount.EndsWith('$')) {
        sc.exe config $ServiceName obj= $ServiceAccount password= '' | Out-Null
    }
    else {
        $credential = Get-Credential -UserName $ServiceAccount -Message 'Worker service account'
        sc.exe config $ServiceName obj= $ServiceAccount password= $credential.GetNetworkCredential().Password | Out-Null
    }
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}

$environment = @(
    'DOTNET_ENVIRONMENT=Production',
    "ConnectionStrings__FurnaceRecords=Server=$SqlServer.$DomainName;Database=FurnaceRecords;Integrated Security=true;TrustServerCertificate=true;Application Name=Furnace Records Worker",
    "Worker__MesAddress=$MesAddress",
    "Worker__CertificateFolder=\\$FilesHost.$DomainName\FurnaceRecords$\Certificates",
    "Worker__SmtpHost=$SmtpHost.$DomainName"
)
for ($i = 0; $i -lt $ReminderRecipients.Count; $i++) {
    $environment += "Worker__ReminderRecipients__$($i)=$($ReminderRecipients[$i])"
}
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -PropertyType MultiString -Force -Value $environment | Out-Null

if (-not [System.Diagnostics.EventLog]::SourceExists('Furnace Records Worker')) {
    New-EventLog -LogName Application -Source 'Furnace Records Worker'
}

Start-Service $ServiceName
Get-Service $ServiceName
