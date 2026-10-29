<#
.SYNOPSIS
    Uploads the shared data source and the report definitions to Reporting Services 2008 R2
    (ReportService2005 SOAP endpoint, basic authentication with a local report server account).
#>
param(
    [ValidateSet('Production', 'Lab')] [string] $Environment = 'Production',
    [string] $SecretsFile
)
. (Join-Path $PSScriptRoot 'Common.ps1')
$settings = Get-DeploySettings $Environment $SecretsFile
if ($settings.LabStubs) {
    Write-Host 'The lab report server stand-in renders placeholder documents; nothing to upload.'
    return
}

$credential = New-Object System.Management.Automation.PSCredential(
    (Get-Secret $settings 'ReportServerUser' ''),
    (ConvertTo-SecureString (Get-Secret $settings 'ReportServerPassword' '') -AsPlainText -Force))
$rs = New-WebServiceProxy -Uri "$($settings.ReportServer.Url)/ReportService2005.asmx?wsdl" -Credential $credential -Namespace 'Southridge.ReportService'
$folder = $settings.ReportServer.Folder

Write-Step "Folders under $folder"
$path = ''
foreach ($part in $folder.Trim('/').Split('/')) {
    $parent = if ($path) { $path } else { '/' }
    $path = "$path/$part"
    if ($rs.GetItemType($path) -eq 'Unknown') { $rs.CreateFolder($part, $parent, $null) | Out-Null }
}

Write-Step 'Shared data source Clinic'
$definition = New-Object Southridge.ReportService.DataSourceDefinition
$definition.Extension = 'SQL'
$definition.ConnectString = $settings.ReportServer.DataSource
$definition.CredentialRetrieval = 'Store'
$definition.UserName = 'clinic_reports'
$definition.Password = Get-Secret $settings 'ReportsDbPassword' ''
$definition.WindowsCredentials = $false
$definition.Enabled = $true
$rs.CreateDataSource('Clinic', $folder, $true, $definition, $null)

Write-Step 'Reports'
foreach ($rdl in Get-ChildItem (Join-Path $ScenarioRoot 'reports') -Filter '*.rdl') {
    $name = [IO.Path]::GetFileNameWithoutExtension($rdl.Name)
    $warnings = $rs.CreateReport($name, $folder, $true, [IO.File]::ReadAllBytes($rdl.FullName), $null)
    $reference = New-Object Southridge.ReportService.DataSourceReference
    $reference.Reference = "$folder/Clinic"
    $source = New-Object Southridge.ReportService.DataSource
    $source.Name = 'Clinic'
    $source.Item = $reference
    $rs.SetItemDataSources("$folder/$name", @($source))
    Write-Host "    $name$(if ($warnings) { " ($($warnings.Count) warnings)" })"
}
