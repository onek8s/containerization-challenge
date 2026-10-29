#Requires -Version 7.2
# Shared helpers for the deployment scripts. Dot-source: . "$PSScriptRoot\Common.ps1"

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Import-RetailEnvironment {
    param([Parameter(Mandatory)][string] $Path)
    $envData = Import-PowerShellDataFile -Path (Resolve-Path $Path)
    Write-Host "Environment $($envData.Name): SQL $($envData.SqlServer), RabbitMQ $($envData.RabbitMqHost)" -ForegroundColor Cyan
    return $envData
}

function Get-RetailSecret {
    param([Parameter(Mandatory)] $Environment, [Parameter(Mandatory)][string] $Name)
    # Microsoft.PowerShell.SecretManagement with the SecretStore (or KeePass) vault registered on the build agent.
    $value = Get-Secret -Vault $Environment.Vault -Name $Name -AsPlainText -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($value)) { throw "Secret '$Name' not found in vault '$($Environment.Vault)'." }
    return $value
}

function Get-HostName {
    param([Parameter(Mandatory)] $Environment, [Parameter(Mandatory)][string] $HostKey)
    return $Environment.Hosts[$HostKey].Name
}

function Get-PackagePath {
    param([Parameter(Mandatory)][string] $Project, [string] $Root = (Join-Path $PSScriptRoot '..\publish'))
    $zip = Join-Path $Root "$Project.zip"
    if (-not (Test-Path $zip)) { throw "Package $zip missing. Run Publish.ps1 first." }
    return $zip
}

function Get-CertificateThumbprint {
    param([Parameter(Mandatory)] $Environment, [Parameter(Mandatory)] [System.Management.Automation.Runspaces.PSSession] $Session)
    Invoke-Command -Session $Session -ArgumentList $Environment.CertificateSubject -ScriptBlock {
        param($subject)
        $cert = Get-ChildItem Cert:\LocalMachine\My |
            Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date).AddDays(14) -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
        if (-not $cert) { throw "No valid certificate '$subject' in LocalMachine\My on $env:COMPUTERNAME." }
        $cert.Thumbprint
    }
}

function Set-JsonValue {
    # Sets "A:B:0:C" in a hashtable produced by ConvertFrom-Json -AsHashtable.
    param([Parameter(Mandatory)] $Json, [Parameter(Mandatory)][string] $Key, $Value)
    $parts = $Key -split ':'
    $node = $Json
    for ($i = 0; $i -lt $parts.Length - 1; $i++) {
        $p = $parts[$i]
        if ($node -is [System.Collections.IList]) { $node = $node[[int]$p]; continue }
        if (-not $node.ContainsKey($p)) { $node[$p] = [ordered]@{} }
        $node = $node[$p]
    }
    $last = $parts[-1]
    if ($node -is [System.Collections.IList]) { $node[[int]$last] = $Value } else { $node[$last] = $Value }
}

function Get-JsonValue {
    param([Parameter(Mandatory)] $Json, [Parameter(Mandatory)][string] $Key)
    $node = $Json
    foreach ($p in ($Key -split ':')) {
        if ($null -eq $node) { return $null }
        if ($node -is [System.Collections.IList]) { $node = $node[[int]$p] }
        elseif ($node.ContainsKey($p)) { $node = $node[$p] }
        else { return $null }
    }
    return $node
}

function New-AppSettingsContent {
    <#
      Returns appsettings.Production.json of a package with the secrets of the environment filled in.
      The file is then copied to the host and its ACL limited to the application identity.
    #>
    param([Parameter(Mandatory)] $Environment, [Parameter(Mandatory)][string] $Project, [Parameter(Mandatory)][string] $Template)

    $json = Get-Content $Template -Raw | ConvertFrom-Json -AsHashtable

    if ($json.ContainsKey('ConnectionStrings')) {
        foreach ($name in @($json.ConnectionStrings.Keys)) {
            $cs = $json.ConnectionStrings[$name]
            if ($cs -match 'User Id=([^;]+);Password=;') {
                $login = $Matches[1]
                $json.ConnectionStrings[$name] = $cs -replace 'Password=;', ("Password=" + (Get-RetailSecret $Environment "sql-$login") + ';')
            }
            elseif ($cs -match '^mongodb://([^:]+):@') {
                $json.ConnectionStrings[$name] = $cs -replace ':@', (':' + [uri]::EscapeDataString((Get-RetailSecret $Environment "mongo-$($Matches[1])")) + '@')
            }
            $json.ConnectionStrings[$name] = $json.ConnectionStrings[$name] -replace 'sql01\.corp\.local', $Environment.SqlServer
        }
    }

    $mqUser = Get-JsonValue $json 'Messaging:Username'
    if ($mqUser) {
        Set-JsonValue $json 'Messaging:Host' $Environment.RabbitMqHost
        Set-JsonValue $json 'Messaging:VirtualHost' $Environment.RabbitMqVHost
        Set-JsonValue $json 'Messaging:Password' (Get-RetailSecret $Environment "rabbitmq-$mqUser")
    }
    if ((Get-JsonValue $json 'Serilog:WriteTo:1:Name') -eq 'Seq') {
        Set-JsonValue $json 'Serilog:WriteTo:1:Args:apiKey' (Get-RetailSecret $Environment 'seq-apikey')
    }
    if ($Environment.ClientIds.ContainsKey($Project) -and $json.ContainsKey('ServiceIdentity')) {
        Set-JsonValue $json 'ServiceIdentity:ClientId' $Environment.ClientIds[$Project]
        Set-JsonValue $json 'ServiceIdentity:ClientSecret' (Get-RetailSecret $Environment ('entra-' + ($Project -replace '^VanArsdel\.', '' -replace '\.Api$', '').ToLowerInvariant()))
    }
    if ($Environment.Settings.ContainsKey($Project)) {
        foreach ($entry in $Environment.Settings[$Project].GetEnumerator()) {
            $value = $entry.Value
            if ($value -is [string] -and $value.StartsWith('vault:')) { $value = Get-RetailSecret $Environment $value.Substring(6) }
            Set-JsonValue $json $entry.Key $value
        }
    }
    return ($json | ConvertTo-Json -Depth 20)
}

function Copy-PackageToHost {
    param(
        [Parameter(Mandatory)] [System.Management.Automation.Runspaces.PSSession] $Session,
        [Parameter(Mandatory)][string] $Package,
        [Parameter(Mandatory)][string] $Target,
        [Parameter(Mandatory)][string] $Settings)

    $remoteZip = "C:\Windows\Temp\$([IO.Path]::GetFileName($Package))"
    Copy-Item -Path $Package -Destination $remoteZip -ToSession $Session -Force
    Invoke-Command -Session $Session -ArgumentList $remoteZip, $Target, $Settings -ScriptBlock {
        param($zip, $target, $settings)
        $staging = "$target.new"
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
        Expand-Archive -Path $zip -DestinationPath $staging -Force
        Set-Content -Path (Join-Path $staging 'appsettings.Production.json') -Value $settings -Encoding utf8NoBOM
        Remove-Item $zip -Force
    }
}

function Wait-HealthEndpoint {
    param([Parameter(Mandatory)][string] $Url, [int] $TimeoutSeconds = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10
            if ($r.StatusCode -eq 200) { Write-Host "  $Url healthy" -ForegroundColor Green; return }
        }
        catch { Start-Sleep -Seconds 5 }
    } while ((Get-Date) -lt $deadline)
    throw "$Url did not become healthy in $TimeoutSeconds s."
}
