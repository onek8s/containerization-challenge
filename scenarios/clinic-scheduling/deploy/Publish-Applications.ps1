<#
.SYNOPSIS
    Builds the solution for the environment's configuration and stages every deployable under .\out\<Environment>:
    the four IIS applications (Web.config transformed by the publish pipeline, tokens replaced) and the
    interface engine, billing batch and reminders programs (App.config values set from the settings).
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production',
    [string] $SecretsFile,
    [string] $OutputRoot = (Join-Path $PSScriptRoot 'out')
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings = Get-DeploySettings $Environment $SecretsFile
$msbuild = Get-MSBuild
$out = New-Folder (Join-Path $OutputRoot $settings.Name)
$solution = Join-Path $ScenarioRoot 'Southridge.Clinic.sln'

Write-Step "Restore and build $($settings.Configuration)"
& $msbuild $solution /t:Restore /p:Configuration=$($settings.Configuration) /v:minimal /nologo
& $msbuild $solution /t:Build /p:Configuration=$($settings.Configuration) /v:minimal /nologo /m
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$tokens = @{
    WEB_DB_PASSWORD          = Get-Secret $settings 'WebDbPassword' 'dev-web'
    SERVICES_DB_PASSWORD     = Get-Secret $settings 'ServicesDbPassword' 'dev-services'
    PORTAL_DB_PASSWORD       = Get-Secret $settings 'PortalDbPassword' 'dev-portal'
    PORTAL_API_KEY           = Get-Secret $settings 'PortalApiKey' 'lab-portal'
    INTERNAL_SERVICE_KEY     = Get-Secret $settings 'InternalServiceKey' 'lab-internal'
    SERVICE_CERT_PASSWORD    = Get-Secret $settings 'ServiceCertificatePassword' 'dev-cert'
    MACHINE_VALIDATION_KEY   = Get-Secret $settings 'MachineValidationKey' ('0' * 128)
    MACHINE_DECRYPTION_KEY   = Get-Secret $settings 'MachineDecryptionKey' ('0' * 64)
}

foreach ($site in $settings.Sites) {
    Write-Step "Publish $($site.Project)"
    $project = Join-Path $ScenarioRoot "src\$($site.Project)\$($site.Project).csproj"
    $target = Join-Path $out "sites\$($site.Project)"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    & $msbuild $project /p:Configuration=$($settings.Configuration) /p:DeployOnBuild=true /p:WebPublishMethod=FileSystem `
        /p:PublishUrl=$target /p:DeleteExistingFiles=true /p:ExcludeApp_Data=false /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "Publishing $($site.Project) failed." }
    Set-ConfigTokens (Join-Path $target 'Web.config') $tokens
}

$pfx = if ($settings.Secrets.ContainsKey('ServiceCertificatePfx')) { $settings.Secrets.ServiceCertificatePfx } else { '' }
if ($pfx) {
    Copy-Item $pfx (Join-Path $out 'sites\Southridge.Clinic.Services\App_Data\clinic-svc.pfx') -Force
}

function Copy-Program([string] $Project, [string] $Name) {
    $source = Join-Path $ScenarioRoot "src\$Project\bin\$($settings.Configuration)\net452"
    $target = Join-Path $out "apps\$Name"
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    New-Folder $target | Out-Null
    Copy-Item (Join-Path $source '*') $target -Recurse -Exclude '*.pdb', '*.xml'
    return $target
}

$dataRoot = Get-LocalPath $settings.DataRoot

Write-Step 'Stage interface engine'
$engine = Copy-Program 'Southridge.Clinic.InterfaceEngine' 'InterfaceEngine'
$engineConfig = Join-Path $engine 'SouthridgeInterfaceEngine.exe.config'
Set-AppConfig $engineConfig @{ 'Clinic:Environment' = $settings.Name } @{
    Clinic = Get-ConnectionString $settings $settings.Sql.Database 'clinic_interface' (Get-Secret $settings 'InterfaceDbPassword' 'dev-interface') 'Clinic Interface Engine'
}
[xml] $xml = Get-Content $engineConfig
foreach ($sender in $xml.configuration.interfaceEngine.senders.add) {
    $sender.SetAttribute('host', $(if ($sender.partner -eq 'LAB') { $settings.InterfaceEngine.LabHost } else { $settings.InterfaceEngine.HospitalHost }))
}
$xml.Save($engineConfig)

Write-Step 'Stage billing batch'
$billing = Copy-Program 'Southridge.Clinic.BillingBatch' 'BillingBatch'
$billingSettings = @{
    'Clinic:Environment'      = $settings.Name
    'Billing:WorkFolder'      = Join-Path $dataRoot 'Billing\Work'
    'Billing:StatementFolder' = Join-Path $dataRoot 'Billing\Statements'
    'Billing:ReportFolder'    = Join-Path $dataRoot 'Billing\Reports'
    'Billing:Transport'       = $settings.Clearinghouse.Transport
    'Billing:Production'      = ($settings.Name -eq 'Production').ToString().ToLowerInvariant()
}
if ($settings.Clearinghouse.Transport -eq 'Sftp') {
    $billingSettings['Billing:SftpHost'] = $settings.Clearinghouse.SftpHost
    $billingSettings['Billing:SftpPort'] = $settings.Clearinghouse.SftpPort
    $billingSettings['Billing:SftpUser'] = $settings.Clearinghouse.SftpUser
    $billingSettings['Billing:SftpPassword'] = Get-Secret $settings 'SftpPassword' ''
}
else {
    $billingSettings['Billing:ExchangeOutbound'] = Get-LocalPath $settings.Clearinghouse.Outbound
    $billingSettings['Billing:ExchangeInbound'] = Get-LocalPath $settings.Clearinghouse.Inbound
}
Set-AppConfig (Join-Path $billing 'SouthridgeBilling.exe.config') $billingSettings @{
    Clinic = Get-ConnectionString $settings $settings.Sql.Database 'clinic_batch' (Get-Secret $settings 'BatchDbPassword' 'dev-batch') 'Clinic Billing Batch'
}

Write-Step 'Stage reminders'
$reminders = Copy-Program 'Southridge.Clinic.Reminders' 'Reminders'
Set-AppConfig (Join-Path $reminders 'SouthridgeReminders.exe.config') @{
    'Clinic:Environment' = $settings.Name
    'Clinic:Sms:GatewayUrl'     = $settings.Sms.GatewayUrl
    'Clinic:Sms:Account'        = $settings.Sms.Account
    'Clinic:Sms:Token'          = Get-Secret $settings 'SmsToken' 'lab-sms'
} @{
    Clinic = Get-ConnectionString $settings $settings.Sql.Database 'clinic_batch' (Get-Secret $settings 'BatchDbPassword' 'dev-batch') 'Clinic Reminders'
}

Write-Host "Staged in $out"
