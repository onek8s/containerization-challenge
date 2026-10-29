# Southridge Clinic Systems - lab / training environment (single server clinic-lab, partner stand-ins from tools\Southridge.Clinic.LabStubs).
$settings = & (Join-Path $PSScriptRoot 'environment.ps1')
$settings.Name = 'Lab'
$settings.Configuration = 'Lab'
foreach ($key in @($settings.Hosts.Keys)) { $settings.Hosts[$key] = '10.40.9.10' }
$settings.Hosts['clinic-lab'] = '10.40.9.10'
$settings.Sql.Server = 'clinic-lab.corp.local'
$settings.Sql.DemoData = $true
$settings.InterfaceEngine.HospitalHost = 'localhost'
$settings.InterfaceEngine.LabHost = 'localhost'
$settings.Clearinghouse = @{ Transport = 'Folder'; Outbound = 'Southridge\Exchange\to-clearinghouse'; Inbound = 'Southridge\Exchange\from-clearinghouse' }
$settings.Sms = @{ GatewayUrl = 'http://clinic-lab.corp.local:8096/sms/send'; Account = 'SMG-LAB' }
$settings.ReportServer.Url = 'http://clinic-lab.corp.local:8095/ReportServer'
$settings.LabStubs = $true
$settings
