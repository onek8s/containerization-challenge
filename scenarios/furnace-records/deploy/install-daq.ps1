<#
.SYNOPSIS
    Installs the Furnace Records acquisition service on the acquisition server.
.DESCRIPTION
    Prerequisites on this server:
      - Calvera TCX boards installed, TCX SDK 4.3 (TCX_SDK_4.3.msi) with the board driver
      - .NET 8 and ASP.NET Core 8 runtime (x64)
      - Remote access to the Vantor FurnaceLink OPC server on the furnace HMI: the service account needs
        DCOM remote launch/activation and remote access on the HMI (dcomcnfg, My Computer and Vantor.FurnaceLink.DA.2),
        and the HMI must allow callbacks from this server (Windows Firewall rule for DCOM on both machines)
    Publish first:
      dotnet publish src\FurnaceRecords.Daq -c Release -r win-x64 --self-contained false -o publish\daq
.EXAMPLE
    .\install-daq.ps1 -PublishPath ..\publish\daq -ServiceAccount 'CORP\svc-furnace-daq$'
.EXAMPLE
    .\install-daq.ps1 -PublishPath ..\publish\daq -ServiceAccount 'CORP\svc-furnace-daq$' -Simulator
#>
param(
    [Parameter(Mandatory)] [string] $PublishPath,
    [string] $InstallPath = 'C:\Services\FurnaceRecords.Daq',
    [Parameter(Mandatory)] [string] $ServiceAccount,
    [string] $DomainName = 'corp.local',
    [string] $SqlServer = 'sql01',
    [string] $HmiHost = 'furnace-hmi01',
    [string] $ServiceName = 'FurnaceRecords.Daq',
    [int] $Port = 5200,
    # Runs the simulated furnaces instead of the boards and the OPC server.
    [switch] $Simulator
)

$ErrorActionPreference = 'Stop'

if (-not $Simulator) {
    $clsid = 'HKLM:\SOFTWARE\Classes\CLSID\{5C2E9A42-7B1D-4F6A-9C3E-1A8D2F04B731}\InprocServer32'
    if (-not (Test-Path $clsid)) {
        throw 'TCX SDK not registered. Install TCX_SDK_4.3.msi first.'
    }
    $driver = Get-Service tcx3216 -ErrorAction SilentlyContinue
    if (-not $driver -or $driver.Status -ne 'Running') {
        throw 'TCX board driver (tcx3216) is not running. Check the boards in Device Manager.'
    }
    $boards = @(Get-PnpDevice -PresentOnly -FriendlyName 'Calvera TCX*' -ErrorAction SilentlyContinue)
    Write-Host "TCX boards found: $($boards.Count)"
    if ($boards.Count -eq 0) {
        throw 'No TCX board found.'
    }
    if (-not (Test-NetConnection "$HmiHost.$DomainName" -Port 135 -InformationLevel Quiet)) {
        Write-Warning "RPC endpoint mapper on $HmiHost.$DomainName is not reachable; the OPC server connection will fail."
    }
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force
}
Copy-Item (Join-Path $PublishPath '*') $InstallPath -Recurse -Force
$exe = Join-Path $InstallPath 'FurnaceRecords.Daq.exe'

if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    # Group managed service accounts have no password: sc.exe with password= empty.
    if ($ServiceAccount.EndsWith('$')) {
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Furnace Records Acquisition' `
            -Description 'Records furnace thermocouples and controller events for Furnace Records.' -StartupType Automatic | Out-Null
        sc.exe config $ServiceName obj= $ServiceAccount password= '' | Out-Null
    }
    else {
        $credential = Get-Credential -UserName $ServiceAccount -Message 'Acquisition service account'
        New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName 'Furnace Records Acquisition' `
            -Description 'Records furnace thermocouples and controller events for Furnace Records.' -StartupType Automatic -Credential $credential | Out-Null
    }
    sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/30000/restart/120000 | Out-Null
}

$envKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$environment = @(
    'ASPNETCORE_ENVIRONMENT=Production',
    "ConnectionStrings__FurnaceRecords=Server=$SqlServer.$DomainName;Database=FurnaceRecords;Integrated Security=true;TrustServerCertificate=true;Application Name=Furnace Records Acquisition",
    "Kestrel__Endpoints__Http__Url=http://*:$Port"
)
if ($Simulator) {
    $environment += 'Acquisition__Provider=Simulator'
}
for ($i = 0; $i -lt 2; $i++) {
    $environment += "Acquisition__Furnaces__$($i)__OpcHost=$HmiHost.$DomainName"
}
New-ItemProperty -Path $envKey -Name Environment -PropertyType MultiString -Force -Value $environment | Out-Null

if (-not [System.Diagnostics.EventLog]::SourceExists('Furnace Records Acquisition')) {
    New-EventLog -LogName Application -Source 'Furnace Records Acquisition'
}

$rule = "Furnace Records Acquisition $Port"
if (-not (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $rule -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}

Start-Service $ServiceName
Start-Sleep -Seconds 10
try {
    Invoke-RestMethod "http://localhost:$Port/health" | ConvertTo-Json -Depth 5
}
catch {
    Write-Warning "Health check failed: $($_.Exception.Message). See the Application event log."
}
