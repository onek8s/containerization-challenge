<#
.SYNOPSIS
    Installs one of the hub Windows services from the publish folder: Gateway (invoices-gw),
    Processor (invoices-proc) or ErpLink (invoices-erp). Run as administrator on the target server.
    Message Queuing must be installed first (Install-Queues.ps1).
.EXAMPLE
    .\Install-HubService.ps1 -Role Gateway   -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-gw
    .\Install-HubService.ps1 -Role Processor -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-proc -ZoneName corp.local
    .\Install-HubService.ps1 -Role ErpLink   -PublishPath C:\Build\SupplierInvoices -ServiceAccount CORP\svc-inv-erp
#>
param(
    [Parameter(Mandatory)] [ValidateSet('Gateway', 'Processor', 'ErpLink')] [string]$Role,
    [Parameter(Mandatory)] [string]$PublishPath,
    [Parameter(Mandatory)] [string]$ServiceAccount,
    [string]$ZoneName = 'corp.local',
    [string]$InstallRoot = 'C:\Program Files\Wingtip\Supplier Invoices',
    [string]$DataRoot = 'D:\InvoiceHub',
    [hashtable]$Settings = @{}
)

$ErrorActionPreference = 'Stop'

$definition = @{
    Gateway   = @{ Name = 'InvoiceGateway';   Display = 'Wingtip Invoice Gateway';   Exe = 'Invoices.Gateway';   Ports = @();     Description = 'Collects supplier invoice files from FTP and the drop share.' }
    Processor = @{ Name = 'InvoiceProcessor'; Display = 'Wingtip Invoice Processor'; Exe = 'Invoices.Processor'; Ports = @(8096); Description = 'Parses, validates and matches supplier invoices.' }
    ErpLink   = @{ Name = 'InvoiceErpLink';   Display = 'Wingtip Invoice ERP Link';  Exe = 'Invoices.ErpLink';   Ports = @(8095); Description = 'ERP lookups and accounts payable import files.' }
}[$Role]

$serviceName = $definition.Name
$installPath = Join-Path $InstallRoot $definition.Exe
$credential = Get-Credential -UserName $ServiceAccount -Message "Password for $serviceName"
$password = $credential.GetNetworkCredential().Password

if (Get-Service $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -Force
}

New-Item -ItemType Directory -Force -Path $installPath, "$DataRoot\Logs", "$DataRoot\Work" | Out-Null
$config = Join-Path $installPath "$($definition.Exe).exe.config"
$keepConfig = Test-Path $config
if ($keepConfig) { Copy-Item $config "$config.bak" -Force }
robocopy (Join-Path $PublishPath $definition.Exe) $installPath /E /NFL /NDL /NJH /NJS /XF *.pdb | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying $($definition.Exe) failed." }
if ($keepConfig) { Copy-Item "$config.bak" $config -Force }
icacls $DataRoot /grant "${ServiceAccount}:(OI)(CI)M" | Out-Null

# Configuration: zone name in host names and extra settings.
[xml]$xml = Get-Content $config
if ($ZoneName -ne 'corp.local') {
    $xml.InnerXml = $xml.InnerXml.Replace('.corp.local', ".$ZoneName")
}
# Log files and the work folder under the data root.
if ($DataRoot -ne 'D:\InvoiceHub') {
    $xml.InnerXml = $xml.InnerXml.Replace('D:\InvoiceHub', $DataRoot)
}
if (-not $Settings.ContainsKey('WorkFolder')) { $Settings['WorkFolder'] = "$DataRoot\Work" }
foreach ($key in $Settings.Keys) {
    $node = $xml.configuration.appSettings.add | Where-Object { $_.key -eq $key }
    if ($node) { $node.value = [string]$Settings[$key] }
    else {
        $add = $xml.CreateElement('add'); $add.SetAttribute('key', $key); $add.SetAttribute('value', [string]$Settings[$key])
        $xml.configuration.appSettings.AppendChild($add) | Out-Null
    }
}
$xml.Save($config)

$exe = Join-Path $installPath "$($definition.Exe).exe"
& $exe install-diagnostics
if ($LASTEXITCODE -ne 0) { throw 'Registering the event source and performance counters failed.' }

# Log on as a service right.
$sid = (New-Object System.Security.Principal.NTAccount($ServiceAccount)).Translate([System.Security.Principal.SecurityIdentifier]).Value
$cfg = Join-Path $env:TEMP 'secpol.cfg'
secedit /export /cfg $cfg /areas USER_RIGHTS | Out-Null
$lines = Get-Content $cfg -Encoding Unicode
$line = $lines | Where-Object { $_ -like 'SeServiceLogonRight*' }
if (-not $line) {
    $lines = $lines -replace '^\[Privilege Rights\]$', "[Privilege Rights]`r`nSeServiceLogonRight = *$sid"
}
elseif ($line -notlike "*$sid*") {
    $lines = $lines -replace [regex]::Escape($line), "$line,*$sid"
}
Set-Content $cfg $lines -Encoding Unicode
secedit /configure /db "$env:TEMP\secpol.sdb" /cfg $cfg /areas USER_RIGHTS | Out-Null
Remove-Item $cfg -ErrorAction SilentlyContinue

if (-not (Get-Service $serviceName -ErrorAction SilentlyContinue)) {
    sc.exe create $serviceName binPath= "`"$exe`"" start= delayed-auto obj= $ServiceAccount password= $password DisplayName= $definition.Display | Out-Null
}
else {
    sc.exe config $serviceName binPath= "`"$exe`"" obj= $ServiceAccount password= $password | Out-Null
}
sc.exe description $serviceName $definition.Description | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
sc.exe config $serviceName depend= MSMQ/LanmanWorkstation | Out-Null

foreach ($port in $definition.Ports) {
    $rule = "$serviceName TCP $port"
    if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $port -Program $exe -Action Allow | Out-Null
    }
}

if ($Role -eq 'Processor') {
    # Clients authenticate the control endpoint with Kerberos against host/invoices-proc.<zone>.
    setspn -S "host/invoices-proc.$ZoneName" $env:COMPUTERNAME | Out-Null
}

if ($Role -eq 'Gateway') {
    Write-Host "Enter the FTP password in $config (FtpPassword) and run: `"$exe`" protect-config"
}

Start-Service $serviceName
Get-Service $serviceName
