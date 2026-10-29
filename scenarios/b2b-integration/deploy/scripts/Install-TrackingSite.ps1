<#
    Customer tracking site (forms authentication, SQL logins) on the DMZ web server, published by the reverse
    proxy as /tracking/ on the B2B public host. The server is not in the domain: remoting uses HTTPS and the
    DMZ administrator from the secrets file. The connection strings get the SQL passwords and the farm machine
    key, and both sections are encrypted with aspnet_regiis -pef (RSA key container of the farm).
#>
param(
    [Parameter(Mandatory)] [string] $Environment,
    [Parameter(Mandatory)] [hashtable] $Settings,
    [Parameter(Mandatory)] [hashtable] $Secrets
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'B2BDeploy.psm1')

$src = Join-Path (Get-B2BDeployRoot) '..\src'
$credential = $null
if ($Environment -in 'TEST', 'PROD') {
    $password = ConvertTo-SecureString (Get-B2BSecret $Secrets 'DmzAdminPassword') -AsPlainText -Force
    $credential = New-Object pscredential ((Get-B2BSecret $Secrets 'DmzAdminUser'), $password)
}

# SQL Server is reached through the DMZ firewall on 1433 by its FQDN
$sql = if ($Settings.SqlServer -eq 'localhost') { 'localhost' } else { '{0}.{1},1433' -f $Settings.SqlServer, $Settings.DnsInternalZone }

Publish-B2BWebApplication -Server $Settings.DmzWebServer -ProjectFolder (Join-Path $src 'VanArsdel.Integration.Tracking') -Site 'B2BTracking' `
    -HostHeader $Settings.B2BPublicHost -Environment $Environment -UseHttps (Test-B2BTrue $Settings.UseHttps) `
    -CertificateThumbprint $Settings.CertificateThumbprint -Credential $credential `
    -ConfigureWebConfig {
        param($xml)
        $integration = 'Data Source={0};Initial Catalog={1};User ID={2};Password={3};Encrypt=True;Application Name=B2B.Tracking' -f `
            $sql, $Settings.IntegrationDatabase, $Settings.TrackingSqlLogin, (Get-B2BSecret $Secrets 'TrackingSqlPassword')
        $bam = 'Data Source={0};Initial Catalog={1};User ID={2};Password={3};Encrypt=True;Application Name=B2B.Tracking' -f `
            $sql, $Settings.BamDatabase, $Settings.TrackingBamSqlLogin, (Get-B2BSecret $Secrets 'TrackingBamSqlPassword')
        foreach ($cs in $xml.configuration.connectionStrings.add) {
            if ($cs.name -eq 'VanArsdelIntegration') { $cs.SetAttribute('connectionString', $integration) }
            if ($cs.name -eq 'BAMPrimaryImport') { $cs.SetAttribute('connectionString', $bam) }
        }
        $key = $xml.configuration.'system.web'.machineKey
        $key.SetAttribute('validationKey', (Get-B2BSecret $Secrets 'TrackingValidationKey'))
        $key.SetAttribute('decryptionKey', (Get-B2BSecret $Secrets 'TrackingDecryptionKey'))
    } `
    -PostDeploy {
        param($path)
        $regiis = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'aspnet_regiis.exe'
        foreach ($section in 'connectionStrings', 'system.web/machineKey') {
            & $regiis -pef $section $path -prov 'B2BFarmRsaProvider' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "aspnet_regiis -pef $section failed on $env:COMPUTERNAME." }
            "encrypted $section"
        }
    }

Write-B2BLog ('Reverse proxy rule: https://{0}{1} -> {2}' -f $Settings.B2BPublicHost, $Settings.TrackingPublicPath, $Settings.DmzWebServer)
