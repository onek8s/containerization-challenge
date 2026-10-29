# Shared helpers for the Lucerne Project Sites deploy scripts.
# The farm scripts run in the SharePoint 2016 Management Shell on a farm server as the farm admin.
# The add-in site, service and database scripts run elevated on their target servers.

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Write-Step {
    param([Parameter(Mandatory = $true)] [string] $Message)
    Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $Message) -ForegroundColor Cyan
}

function Get-ProjectSitesSettings {
    param([string] $Path = (Join-Path $PSScriptRoot 'Settings.psd1'))
    # Import-PowerShellDataFile needs PowerShell 5; the farm servers still had 4.0 when this was written.
    $text = Get-Content -Path $Path -Raw
    return (Invoke-Expression $text)
}

function Get-ProjectSitesDrop {
    param(
        [Parameter(Mandatory = $true)] [hashtable] $Settings,
        [string] $Version
    )
    if (-not $Version) {
        $latest = Get-ChildItem -Path $Settings.DropRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if (-not $latest) { throw "No drops found under $($Settings.DropRoot). Run Publish.ps1 first." }
        return $latest.FullName
    }
    $path = Join-Path $Settings.DropRoot $Version
    if (-not (Test-Path $path)) { throw "Drop $Version not found under $($Settings.DropRoot)." }
    return $path
}

function Assert-Elevated {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an elevated PowerShell session.'
    }
}

function Assert-SharePointShell {
    if (-not (Get-PSSnapin -Name Microsoft.SharePoint.PowerShell -ErrorAction SilentlyContinue)) {
        Add-PSSnapin Microsoft.SharePoint.PowerShell
    }
    $farm = Get-SPFarm
    if ($farm.BuildVersion.Major -lt 16) {
        throw "SharePoint Server 2016 is required (farm build $($farm.BuildVersion))."
    }
}

function Wait-SolutionJob {
    # Install-SPSolution / Uninstall-SPSolution create a one-time timer job per server; wait for it to finish
    # before activating features, otherwise the feature definitions are not yet in the 16 hive.
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [int] $TimeoutSeconds = 600
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        Start-Sleep -Seconds 5
        $solution = Get-SPSolution -Identity $Name
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for the deployment job of $Name." }
    } while ($solution.JobExists)

    if ($solution.LastOperationResult -notmatch 'Succeeded') {
        throw "Deployment of $Name ended with $($solution.LastOperationResult): $($solution.LastOperationDetails)"
    }
    Write-Step "$Name deployed ($($solution.LastOperationResult))"
}

function Enable-ProjectSitesFeature {
    param(
        [Parameter(Mandatory = $true)] [string] $Identity,
        [string] $Url,
        [switch] $Force
    )
    $definition = Get-SPFeature -Identity $Identity -ErrorAction SilentlyContinue
    if (-not $definition) { throw "Feature $Identity is not installed in the farm." }

    $active = if ($Url) { Get-SPFeature -Identity $Identity -Web $Url -ErrorAction SilentlyContinue }
    if ($definition.Scope -eq 'Site' -and $Url) { $active = Get-SPFeature -Identity $Identity -Site $Url -ErrorAction SilentlyContinue }
    if ($definition.Scope -eq 'WebApplication' -and $Url) { $active = Get-SPFeature -Identity $Identity -WebApplication $Url -ErrorAction SilentlyContinue }
    if ($definition.Scope -eq 'Farm') { $active = Get-SPFeature -Identity $Identity -Farm -ErrorAction SilentlyContinue }

    if ($active -and -not $Force) {
        Write-Step "Feature $($definition.DisplayName) already active$(if ($Url) { " on $Url" })"
        return
    }
    if ($definition.Scope -eq 'Farm') {
        Enable-SPFeature -Identity $Identity -Force:$Force -Confirm:$false
    } else {
        Enable-SPFeature -Identity $Identity -Url $Url -Force:$Force -Confirm:$false
    }
    Write-Step "Activated $($definition.DisplayName)$(if ($Url) { " on $Url" })"
}

function Set-ProjectSitesAppPool {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [string] $UserName,
        [Parameter(Mandatory = $true)] [securestring] $Password
    )
    Import-Module WebAdministration
    $path = "IIS:\AppPools\$Name"
    if (-not (Test-Path $path)) { New-WebAppPool -Name $Name | Out-Null }
    Set-ItemProperty $path -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty $path -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty $path -Name processModel.loadUserProfile -Value $true
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))
    # The pool runs as a domain account: it needs the private key of the S2S signing certificate.
    Set-ItemProperty $path -Name processModel -Value @{ identityType = 3; userName = $UserName; password = $plain }
    Set-ItemProperty $path -Name recycling.periodicRestart.time -Value '00:00:00'
    Set-ItemProperty $path -Name recycling.periodicRestart.schedule -Value @{ value = '03:15:00' }
}

function Invoke-SqlScript {
    param(
        [Parameter(Mandatory = $true)] [string] $Server,
        [Parameter(Mandatory = $true)] [string] $Path,
        [string[]] $Variables = @()
    )
    $arguments = @('-S', $Server, '-E', '-b', '-i', $Path)
    foreach ($v in $Variables) { $arguments += @('-v', $v) }
    & sqlcmd.exe @arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for $Path (exit $LASTEXITCODE)." }
}

Export-ModuleMember -Function *
